using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.HomeGame;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class HomeGameTests : IAsyncLifetime
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"home-game-{Guid.NewGuid():N}.db");
    private readonly Guid alice = Guid.NewGuid();
    private readonly Guid bob = Guid.NewGuid();
    private readonly Guid teacher = Guid.NewGuid();
    private readonly Broker broker = new();
    private ServiceProvider services = null!;
    private HomeRunnerClient runner = null!;
    private HomeRunCoordinator runs = null!;
    private readonly IOptions<HomeGameOptions> options = Options.Create(new HomeGameOptions
    {
        RunnerUrl = "http://127.0.0.1:5199/",
        RunnerToken = new string('t', 40)
    });

    public async Task InitializeAsync()
    {
        var configured = new ServiceCollection();
        configured.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={path};Pooling=False"));
        configured.AddScoped<HomeGameStore>();
        services = configured.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        runner = new HomeRunnerClient(new RunnerFactory(broker), options);
        runs = new HomeRunCoordinator(services.GetRequiredService<IServiceScopeFactory>(), runner,
            NullLogger<HomeRunCoordinator>.Instance);
    }

    public async Task DisposeAsync()
    {
        await runs.StopAsync(CancellationToken.None);
        runs.Dispose();
        await services.DisposeAsync();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
    }

    [Fact]
    public async Task Build_receipt_revision_and_undo_use_the_avatar_wallet_and_preserve_account_isolation()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = As(db, alice);
        var avatars = new AvatarsController(db) { ControllerContext = Context(alice, "Student") };
        var avatar = Read(await avatars.Unlock("bip01", default));
        var start = Read(await owner.Get(default));
        Assert.Equal(avatar.Gold, start.Gold);
        Assert.Equal("bip01", start.SelectedAvatarId);
        Read(await avatars.Select(new(AvatarEconomy.StarterId), default));
        Assert.Equal(AvatarEconomy.StarterId, Read(await owner.Get(default)).SelectedAvatarId);
        var purchase = new HomeBuildRequest(Guid.NewGuid(), start.Layout.Revision,
            new("buy", DefinitionId: HomeCatalog.LampId, Placement: new(new(4, 4))));
        var bought = Read(await owner.Build(purchase, default));
        Assert.Equal(start.Gold - 30, bought.Gold);
        Assert.Equal(bought.Gold, Read(await avatars.Get(default)).Gold);
        Assert.Equal(bought.Layout.Revision, Read(await owner.Build(purchase, default)).Layout.Revision);
        Assert.Equal(bought.Gold, Read(await avatars.Get(default)).Gold);
        Assert.Equal(409, Status(await owner.Build(purchase with { OperationId = Guid.NewGuid() }, default)));
        Assert.Equal(409, Status(await owner.Build(purchase with { Action = purchase.Action with { Placement = new(new(5, 4)) } }, default)));
        var undo = Read(await owner.Build(new(Guid.NewGuid(), bought.Layout.Revision, new("undo")), default));
        Assert.Equal(start.Gold, undo.Gold);
        Assert.Equal(undo.Gold, Read(await avatars.Get(default)).Gold);
        Assert.Equal(start.Layout.Items, undo.Layout.Items);
        Assert.Equal(2, await db.HomeGameBuildReceipts.CountAsync());

        var someoneElse = As(db, bob);
        var unrelated = Read(await someoneElse.Get(default));
        Assert.Equal(AvatarEconomy.InitialGold, unrelated.Gold);
        Assert.DoesNotContain(unrelated.Layout.Items, item => start.Layout.Items.Any(owned => owned.Id == item.Id));
        someoneElse.Request.Headers["X-LUP-Apartment-Subject"] = alice.ToString();
        Assert.Equal(409, Status(await someoneElse.Get(default)));
    }

    [Fact]
    public async Task Refunding_a_sold_item_requires_actual_shared_gold_paid_for_it()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = As(db, alice);
        var start = Read(await owner.Get(default));
        var lamp = Read(await owner.Build(new(Guid.NewGuid(), start.Layout.Revision,
            new("buy", DefinitionId: HomeCatalog.LampId, Placement: new(new(4, 4)))), default));
        var purchased = Assert.Single(lamp.Layout.Items, item => !item.Starter);
        Assert.Equal(30, purchased.GoldPaid);
        var sold = Read(await owner.Build(new(Guid.NewGuid(), lamp.Layout.Revision, new("sell", purchased.Id)), default));
        Assert.Equal(start.Gold, sold.Gold);
        Assert.DoesNotContain(sold.Layout.Items, item => item.Id == purchased.Id);
        var recovered = Read(await owner.Build(new(Guid.NewGuid(), sold.Layout.Revision, new("undo")), default));
        Assert.Equal(lamp.Gold, recovered.Gold);
        Assert.Contains(recovered.Layout.Items, item => item.Id == purchased.Id && item.GoldPaid == 30);

        var row = await db.HomeGameProfiles.SingleAsync();
        var oldData = JsonNode.Parse(row.StateJson)!.AsObject();
        oldData["coins"] = 100;
        var oldItem = oldData["layout"]!["items"]!.AsArray().OfType<JsonObject>()
            .Single(item => item["id"]!.GetValue<string>() == purchased.Id);
        oldItem.Remove("goldPaid");
        oldItem["paid"] = 30;
        row.StateJson = oldData.ToJsonString();
        await db.SaveChangesAsync();
        var legacySale = Read(await owner.Build(new(Guid.NewGuid(), recovered.Layout.Revision, new("sell", purchased.Id)), default));
        Assert.Equal(recovered.Gold, legacySale.Gold);
    }

    [Fact]
    public async Task Insufficient_shared_gold_rolls_back_the_item_and_purchase_receipt()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AvatarProfiles.Add(new AvatarProfile { UserSub = alice, Gold = 29 });
        await db.SaveChangesAsync();
        var owner = As(db, alice);
        var start = Read(await owner.Get(default));
        var purchase = new HomeBuildRequest(Guid.NewGuid(), start.Layout.Revision,
            new("buy", DefinitionId: HomeCatalog.LampId, Placement: new(new(4, 4))));
        Assert.Equal(422, Status(await owner.Build(purchase, default)));
        Assert.Empty(await db.HomeGameBuildReceipts.ToArrayAsync());
        var state = Read(await owner.Get(default));
        Assert.Equal(start.Layout.Revision, state.Layout.Revision);
        Assert.Equal(start.Layout.Items, state.Layout.Items);
        Assert.Equal(29, state.Gold);
    }

    [Fact]
    public async Task Free_run_keeps_its_program_and_replay_without_grades_or_wallet_changes()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = As(db, alice);
        var profile = await Prepare(owner);
        Assert.Equal(400, Status(await owner.Start(new(profile.Layout.Revision, "python", 1, Assessment: true), default)));
        Assert.Empty(await db.HomeGameRuns.ToListAsync());
        var first = await Start(owner, profile);
        Assert.Equal(0, first.Run.Task);
        Assert.False(first.Run.Assessment);
        Assert.Equal(404, Status(await As(db, bob).Status(first.Run.Id, default)));
        var paused = Read(await owner.Control(first.Run.Id, new("pause"), default));
        Assert.Equal("paused", paused.Run.Status);
        Assert.Equal(paused.Snapshot!.TimeMs,
            Read(await owner.Step(first.Run.Id, new(paused.Sequence, 20), default)).Snapshot!.TimeMs);
        var resumed = Read(await owner.Control(first.Run.Id, new("resume"), default));
        var completed = await Finish(owner, resumed);
        Assert.Equal("completed", completed.Run.Status);
        Assert.Null(completed.Run.Passed);
        Assert.Empty(completed.Outcomes);
        Assert.Equal(0, completed.GoldAwarded);
        Assert.Equal(1, broker.Activations);
        Assert.Equal(HomeCatalog.FreeScenario.Ticks, completed.Snapshot!.Tick);
        var replay = Read(await owner.Replay(completed.Run.Id, default));
        Assert.Empty(Assert.Single(replay.Scenarios).Windows);
        Assert.True(Assert.Single(replay.Events).ControllerCompleted);

        var reloaded = Read(await owner.Get(default));
        Assert.Equal(profile.Gold, reloaded.Gold);
        Assert.Equal(profile.Layout.Revision, reloaded.Layout.Revision);
        Assert.Empty(reloaded.Progress);
        Assert.Equal(completed.Run.ProgramVersionId, Assert.Single(reloaded.Drafts).VersionId);
        var again = await Finish(owner, await Start(owner, reloaded));
        Assert.Equal("completed", again.Run.Status);
        Assert.Equal(0, again.GoldAwarded);
        Assert.Equal(profile.Gold, Read(await owner.Get(default)).Gold);
    }

    [Fact]
    public async Task Existing_room_progress_and_earned_gold_survive_and_graded_replay_is_read_only()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = As(db, alice);
        var profile = await Prepare(owner);
        var run = await Finish(owner, await Start(owner, profile));
        var row = await db.HomeGameRuns.SingleAsync();
        var stored = HomeGameJson.Read<HomeStoredRun>(row.RunJson);
        var summary = run.Run with { Status = "passed", Task = 1, Assessment = true, Passed = true };
        row.RunJson = HomeGameJson.Write(stored with
        {
            Replay = stored.Replay with { Run = run with { Run = summary, GoldAwarded = 25 } }
        });
        var existing = await db.HomeGameProfiles.SingleAsync();
        var oldData = HomeGameJson.Read<HomeProfileData>(existing.StateJson);
        var oldLayout = oldData.Layout with
        {
            Items = oldData.Layout.Items.Where(item => item.DefinitionId != HomeCatalog.SofaId && item.DefinitionId != HomeCatalog.RugId)
                .Select(item => item.DefinitionId == HomeCatalog.LampId ? item with { Placement = null } : item).ToArray()
        };
        existing.StateJson = HomeGameJson.Write(oldData with
        {
            Layout = oldLayout,
            Progress = [new(1, "P01:first-pass", HomeGameVersion.Content, summary.Id, DateTimeOffset.UtcNow)]
        });
        db.AvatarProfiles.Add(new AvatarProfile { UserSub = alice, Gold = AvatarEconomy.InitialGold + 25 });
        await db.SaveChangesAsync();

        var restored = Read(await owner.Get(default));
        Assert.Equal(oldLayout.Items, restored.Layout.Items);
        Assert.Equal(summary.Id, Assert.Single(restored.Progress).RunId);
        Assert.Equal(AvatarEconomy.InitialGold + 25, restored.Gold);
        var archived = Read(await owner.Replay(summary.Id, default));
        Assert.Equal(25, archived.Run.GoldAwarded);
        Assert.True(archived.Run.Run.Passed);
        Assert.Equal(422, Status(await owner.Start(new(restored.Layout.Revision, "python", RestartRunId: summary.Id), default)));
        Assert.Equal(1, await db.HomeGameRuns.CountAsync());
    }

    [Fact]
    public async Task Wrong_worker_binding_is_rejected_and_layout_revision_invalidates_an_older_run()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = As(db, alice);
        var state = await Prepare(owner);
        broker.FutureTick = true;
        var started = await Start(owner, state);
        var bad = Read(await owner.Step(started.Run.Id, new(started.Sequence), default));
        Assert.Equal("INVALID_BATCH", bad.Run.ErrorCode);
        Assert.Empty(bad.Snapshot!.PoweredAliases);
        Assert.Empty(Read(await owner.Get(default)).Progress);

        broker.FutureTick = false;
        started = await Start(owner, state);
        var oldRevision = state.Layout.Revision;
        state = Read(await owner.SaveProgram(new(oldRevision, "python", "print('changed')"), default));
        Assert.True(state.Layout.Revision > oldRevision);
        Assert.Equal("cancelled", Read(await owner.Status(started.Run.Id, default)).Run.Status);
        Assert.Equal(409, Status(await owner.SaveProgram(new(oldRevision, "python", "print('stale')"), default)));
    }

    [Fact]
    public async Task Builds_respect_paused_avatar_and_only_valid_placements_invalidate_attempt()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = As(db, alice);
        var state = await Prepare(owner);
        var started = await Start(owner, state);
        var advanced = Read(await owner.Step(started.Run.Id, new(started.Sequence, 20), default));
        var cell = Assert.IsType<HomeCell>(advanced.Snapshot!.AvatarCell);
        var purchase = new HomeBuildRequest(Guid.NewGuid(), state.Layout.Revision, new("buy",
            DefinitionId: HomeCatalog.LampId, Placement: new(cell)));
        Assert.Equal(409, Status(await owner.Build(purchase, default)));
        Read(await owner.Control(started.Run.Id, new("pause"), default));
        Assert.Equal(422, Status(await owner.Build(purchase, default)));
        Assert.Equal(AvatarEconomy.InitialGold, Read(await owner.Get(default)).Gold);
        Assert.Equal("paused", Read(await owner.Status(started.Run.Id, default)).Run.Status);
        var safe = purchase with { Action = purchase.Action with { Placement = new(new(10, 8)) } };
        Assert.Equal(AvatarEconomy.InitialGold - 30, Read(await owner.Build(safe, default)).Gold);
        Assert.Equal("cancelled", Read(await owner.Status(started.Run.Id, default)).Run.Status);
    }

    [Fact]
    public async Task Teacher_can_share_starter_code_and_read_help_only_within_own_hold()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "Programmering", CreatedBySub = teacher };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "Hold 1", Forloeb = course };
        db.Hold.Add(hold);
        db.Memberships.Add(new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = alice, DisplayName = "Alice" });
        await db.SaveChangesAsync();

        var denied = AsTeacher(db, bob);
        Assert.Equal(404, Status(await denied.Overview(hold.Id, default)));
        var allowed = AsTeacher(db, teacher);
        Assert.Equal(404, Status(await allowed.Student(hold.Id, bob, default)));
        var settings = new HomeTeacherSettings(0, 0, 0, "csharp",
            new Dictionary<string, string> { ["csharp"] = "// Starter" });
        Assert.Equal(1, Read(await allowed.SaveSettings(hold.Id, settings, default)).Revision);

        var owner = As(db, alice);
        var home = Read(await owner.Get(default));
        Assert.Equal(0, home.HighestAvailableTask);
        Assert.Equal("csharp", Assert.Single(home.Classrooms).SuggestedLanguage);
        var sensor = home.Layout.Items.Single(item => item.DefinitionId == HomeCatalog.DaylightId);
        Read(await owner.Build(new(Guid.NewGuid(), home.Layout.Revision,
            new("place", sensor.Id, Placement: new(new(7, 0), 180, new(7, 0, true)))), default));
        home = await Prepare(owner);
        var historicalRun = await Start(owner, home);
        historicalRun = Read(await owner.Step(historicalRun.Run.Id, new(historicalRun.Sequence), default));
        var help = new HomeHelpRequest(Guid.NewGuid(), hold.Id, 0, "Hvordan læser jeg lux?");
        Read(await owner.Help(help, default));
        Read(await owner.Help(help, default));
        Assert.Equal(historicalRun.Run.Id, Assert.Single(Read(await allowed.Overview(hold.Id, default)).HelpRequests).Run?.Id);
        home = Read(await owner.SaveProgram(new(home.Layout.Revision, "python", "print('newer')"), default));
        var latest = await Start(owner, home);
        var detail = Read(await allowed.Student(hold.Id, alice, default, historicalRun.Run.Id));
        Assert.NotEqual(latest.Run.Id, detail.LastRun?.Run.Id);
        Assert.Equal(historicalRun.Run.ProgramVersionId, detail.RunProgram?.VersionId);
        Assert.Equal(404, Status(await denied.Student(hold.Id, alice, default, historicalRun.Run.Id)));
        Assert.Empty(Read(await As(db, bob).Get(default)).Classrooms);
    }

    private HomeGameController As(AppDbContext db, Guid subject, string role = "Student")
    {
        var context = Context(subject, role);
        context.HttpContext.Request.Headers["X-LUP-Apartment-Subject"] = subject.ToString();
        return new(db, new HomeGameStore(db), runs, runner, options) { ControllerContext = context };
    }

    private HomeTeacherController AsTeacher(AppDbContext db, Guid subject) =>
        new(db, new CourseAccess(db), new HomeGameStore(db), options) { ControllerContext = Context(subject, "Teacher") };

    private static ControllerContext Context(Guid subject, string role) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", subject.ToString()), new(ClaimTypes.Role, role)], "test"))
        }
    };

    private static async Task<HomeGameState> Prepare(HomeGameController controller)
    {
        var home = Read(await controller.Get(default));
        var lamp = home.Layout.Items.Single(item => item.DefinitionId == HomeCatalog.LampId);
        home = Read(await controller.Build(new(Guid.NewGuid(), home.Layout.Revision,
            new("place", lamp.Id, Placement: new(new(5, 4)))), default));
        return Read(await controller.SaveProgram(new(home.Layout.Revision, "python", "import home\nhome.set('desk_lamp', True)"), default));
    }

    private static async Task<HomeRunStatus> Start(HomeGameController controller, HomeGameState home)
    {
        var run = Read(await controller.Start(new(home.Layout.Revision, "python"), default));
        for (var i = 0; i < 100; i++)
        {
            run = Read(await controller.Status(run.Run.Id, default));
            if (run.Run.Status is not ("queued" or "compiling")) break;
            await Task.Delay(10);
        }
        Assert.Equal("running", run.Run.Status);
        return run;
    }

    private static async Task<HomeRunStatus> Finish(HomeGameController controller, HomeRunStatus run)
    {
        for (var i = 0; i < 61 && run.Run.Status == "running"; i++)
            run = Read(await controller.Step(run.Run.Id, new(run.Sequence, 20), default));
        return run;
    }

    private static T Read<T>(ActionResult<T> response) =>
        Assert.IsType<T>(Assert.IsAssignableFrom<ObjectResult>(response.Result).Value);

    private static int? Status<T>(ActionResult<T> response) => response.Result switch
    {
        ObjectResult result => result.StatusCode,
        StatusCodeResult result => result.StatusCode,
        _ => null
    };

    private sealed class RunnerFactory(HttpMessageHandler broker) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(broker, disposeHandler: false);
    }

    // Exercises the gateway state machine and simulator without executing code inside the test API process.
    private sealed class Broker : HttpMessageHandler
    {
        public bool FutureTick { get; set; }
        public int Activations { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Delete)
                return new(HttpStatusCode.OK);

            using var payload = await JsonDocument.ParseAsync(await request.Content!.ReadAsStreamAsync(ct), cancellationToken: ct);
            var value = payload.RootElement;
            var runId = value.GetProperty("runId").GetGuid();
            if (path == "/sessions") return new(HttpStatusCode.OK)
                { Content = JsonContent.Create(new { sessionId = value.GetProperty("sessionId").GetGuid() }) };
            Activations++;
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    runId,
                    controllerId = value.GetProperty("controllerId").GetString(),
                    sequence = value.GetProperty("sequence").GetInt64(),
                    tick = value.GetProperty("tick").GetInt32() + (FutureTick ? 1 : 0),
                    commands = new[] { new { alias = "desk_lamp", action = "setPower", value = true } },
                    done = true
                })
            };
        }
    }
}
