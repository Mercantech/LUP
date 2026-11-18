using System.Text;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Avatars;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.HomeGame;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/me/home-game")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HomeGameController(AppDbContext db, HomeGameStore store, HomeRunCoordinator runs,
    HomeRunnerClient runner, IOptions<HomeGameOptions> options) : ControllerBase
{
    private const string SubjectHeader = "X-LUP-Apartment-Subject";
    private bool IsTeacherSandbox => UserClaims.IsTeacher(User);

    private int AvailableTask(HomeProfileData state, IReadOnlyList<HomeClassroom> classrooms) =>
        IsTeacherSandbox ? HomeCatalog.Tasks.Count : HomeGameStore.AvailableTask(state, classrooms);

    [HttpGet]
    public async Task<ActionResult<HomeGameState>> Get(CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        return Ok(await Bootstrap(subject, await store.GetOrCreate(subject, ct), ct));
    }

    [HttpPost("build")]
    [RequestSizeLimit(8_192)]
    public async Task<ActionResult<HomeGameState>> Build(HomeBuildRequest request, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        if (request.OperationId == Guid.Empty || request.Action is null)
            return Failure(400, "INVALID_BUILD", "Vælg en gyldig byggehandling.");

        var state = await store.GetOrCreate(subject, ct);
        var fingerprint = HomeGameStore.Hash(HomeGameJson.Write(request));
        var previous = await Receipt(subject, request.OperationId, ct);
        if (previous is not null) return await Repeat(previous, fingerprint, ct);
        if (request.Revision != state.Layout.Revision) return ConflictResult();
        using var guard = await runs.GuardBuild(subject, request.Revision, ct);
        if (!guard.Allowed) return Failure(409, "RUN_BUILD_PAUSE_REQUIRED", "Sæt forsøget på pause, før du bygger.");

        var classrooms = await store.Classrooms(subject, ct);
        var availableTask = AvailableTask(state, classrooms);
        HomeBuildResult result;
        if (request.Action.Kind == "undo")
        {
            if (state.Undo is null) return Failure(409, "NO_UNDO", "Der er ingen byggehandling at fortryde.");
            var oldLayout = state.Undo.Layout with { Revision = state.Layout.Revision };
            result = new(oldLayout, state.Undo.GoldDelta, HomeRules.Validate(oldLayout, guard.AvatarCell));
        }
        else
        {
            result = HomeRules.Apply(state.Layout, request.Action, availableTask - 1, guard.AvatarCell, IsTeacherSandbox);
        }

        if (result.Error is { } error) return Failure(422, error.Code, error.Message, error.Cells);
        if (result.Layout is null) return Failure(400, "INVALID_BUILD", "Byggehandlingen kunne ikke gennemføres.");
        var updated = state with
        {
            Layout = result.Layout with { Revision = state.Layout.Revision + 1 },
            Undo = request.Action.Kind == "undo" ? null : new(state.Layout, -result.GoldDelta)
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await store.Write(subject, request.Revision, updated, ct))
        {
            await transaction.RollbackAsync(ct);
            previous = await Receipt(subject, request.OperationId, ct);
            return previous is null ? ConflictResult() : await Repeat(previous, fingerprint, ct);
        }
        if (!await AvatarWallet.TryAdjustGold(db, subject, result.GoldDelta, ct))
        {
            if (result.GoldDelta < 0)
                return Failure(422, "NOT_ENOUGH_GOLD", request.Action.Kind == "undo"
                    ? "Du har brugt noget af guldet fra salget. Der er ikke nok guld til at fortryde."
                    : "Du har ikke nok guld til købet.");
            return Failure(422, "GOLD_LIMIT", "Din guldsaldo kan ikke rumme beløbet.");
        }
        db.HomeGameBuildReceipts.Add(new HomeBuildReceiptRow
        {
            UserSub = subject, OperationId = request.OperationId, RequestHash = fingerprint,
            StateJson = HomeGameJson.Write(updated)
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        guard.Dispose();
        await runs.Invalidate(subject, updated.Layout.Revision);
        return Ok(await Bootstrap(subject, updated, ct));
    }

    [HttpPut("program")]
    [RequestSizeLimit(800_000)]
    public async Task<ActionResult<HomeGameState>> SaveProgram(HomeSaveProgramRequest request, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        if (!HomeGameLanguages.IsKnown(request.Language) || request.Source is null
            || Encoding.UTF8.GetByteCount(request.Source) > HomeGameVersion.MaxSourceBytes)
            return Failure(400, "INVALID_SOURCE", "Vælg et sprog og gem højst 128 KiB kildekode.");
        var state = await store.GetOrCreate(subject, ct);
        if (request.Revision != state.Layout.Revision) return ConflictResult();
        var previous = state.Drafts.SingleOrDefault(draft => draft.Language == request.Language);
        if (previous?.Source == request.Source) return Ok(await Bootstrap(subject, state, ct));

        var draft = new HomeProgramDraft(request.Language, request.Source, Guid.NewGuid(), HomeGameStore.Hash(request.Source));
        var updated = state with
        {
            Layout = state.Layout with { Revision = state.Layout.Revision + 1 },
            Drafts = [.. state.Drafts.Where(saved => saved.Language != request.Language), draft]
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await store.Write(subject, request.Revision, updated, ct)) return ConflictResult();
        db.HomeGamePrograms.Add(new HomeProgramRow
        {
            Id = draft.VersionId, UserSub = subject, Revision = updated.Layout.Revision, SourceJson = HomeGameJson.Write(draft)
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await runs.Invalidate(subject, updated.Layout.Revision);
        return Ok(await Bootstrap(subject, updated, ct));
    }

    [HttpPut("reflection")]
    [RequestSizeLimit(16_384)]
    public async Task<ActionResult<HomeGameState>> Reflect(HomeReflectionRequest request, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        if (request.Text is null || request.Text.Length > 2_000)
            return Failure(400, "INVALID_REFLECTION", "Skriv højst 2.000 tegn i forklaringen.");
        var state = await store.GetOrCreate(subject, ct);
        if (request.Revision != state.Layout.Revision) return ConflictResult();
        var classrooms = await store.Classrooms(subject, ct);
        if (AvailableTask(state, classrooms) < 6)
            return Failure(403, "TASK_LOCKED", "Åbn opgave 6 for at gemme din forklaring.");
        if (state.Reflection == request.Text.Trim()) return Ok(await Bootstrap(subject, state, ct, classrooms));
        var updated = state with
        {
            Layout = state.Layout with { Revision = state.Layout.Revision + 1 },
            Reflection = request.Text.Trim()
        };
        if (!await store.Write(subject, request.Revision, updated, ct)) return ConflictResult();
        await runs.Invalidate(subject, updated.Layout.Revision);
        return Ok(await Bootstrap(subject, updated, ct, classrooms));
    }

    [HttpPost("help")]
    [RequestSizeLimit(16_384)]
    public async Task<ActionResult<HomeHelpEntry>> Help(HomeHelpRequest request, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        if (request.OperationId == Guid.Empty || request.Task < 1 || request.Task > HomeCatalog.Tasks.Count
            || string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2_000)
            return Failure(400, "INVALID_HELP", "Vælg opgave og skriv et spørgsmål på højst 2.000 tegn.");
        if (!await db.Memberships.AsNoTracking().AnyAsync(m => m.HoldId == request.HoldId && m.UserSub == subject, ct))
            return NotFound();
        var hash = HomeGameStore.Hash(HomeGameJson.Write(request));
        var last = await store.LatestRun(subject, ct);
        var entry = new HomeHelpEntry(request.OperationId, subject, request.Task, request.Question.Trim(),
            last?.Run.Run, DateTimeOffset.UtcNow);
        var json = HomeGameJson.Write(entry);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO home_game_help_requests ("Id", "UserSub", "HoldId", "RequestHash", "DataJson")
            VALUES ({request.OperationId}, {subject}, {request.HoldId}, {hash}, {json})
            ON CONFLICT ("UserSub", "Id") DO NOTHING
            """, ct);
        var saved = await db.HomeGameHelpRequests.AsNoTracking()
            .SingleAsync(row => row.UserSub == subject && row.Id == request.OperationId, ct);
        return saved.RequestHash == hash
            ? Ok(HomeGameJson.Read<HomeHelpEntry>(saved.DataJson))
            : Failure(409, "OPERATION_REUSED", "Denne handling er allerede brugt til en anden anmodning.");
    }

    [HttpPost("runs")]
    [RequestSizeLimit(4_096)]
    public async Task<ActionResult<HomeRunStatus>> Start(HomeStartRunRequest request, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        if (!HomeGameLanguages.IsKnown(request.Language) || request.Task < 1 || request.Task > HomeCatalog.Tasks.Count
            || request.Speed is not (1 or 4 or 10) || !KnownIntent(request.Intent)
            || request.ScenarioId is { } scenario && scenario != "free" && !HomeCatalog.Scenarios.ContainsKey(scenario))
            return Failure(400, "INVALID_RUN", "Vælg et sprog, en opgave og et kendt scenarie.");
        var state = await store.GetOrCreate(subject, ct);
        if (request.Revision != state.Layout.Revision) return ConflictResult();
        var classrooms = await store.Classrooms(subject, ct);
        if (request.Task > AvailableTask(state, classrooms))
            return Failure(403, "TASK_LOCKED", "Afslut den foregående opgave for at åbne denne.");
        var controller = state.Layout.Items.FirstOrDefault(item => item.DefinitionId == HomeCatalog.ControllerId
            && item.Placement is not null);
        if (controller is null)
            return Failure(422, "CONTROLLER_NOT_INSTALLED", "Placér hjemmecontrolleren for at køre kode.");
        var program = state.Drafts.SingleOrDefault(draft => draft.Language == request.Language);
        if (program is null || string.IsNullOrWhiteSpace(program.Source))
            return Failure(422, "MISSING_PROGRAM", "Gem et program i det valgte sprog før kørslen.");

        var task = HomeCatalog.Tasks[request.Task - 1];
        if (request.Assessment && HomeRules.AssessmentPrerequisites(state.Layout, task) is { } requirement)
            return Failure(422, requirement.Code, requirement.Message, requirement.Cells);
        IReadOnlyList<HomeScenario> scenarios;
        if (request.RestartRunId is Guid restartId)
        {
            var previous = await store.FindRun(subject, restartId, ct);
            if (previous is null) return NotFound();
            if (previous.Run.Run.Assessment != request.Assessment || previous.Run.Run.Task != request.Task
                || previous.Run.Run.ContentVersion != HomeGameVersion.Content)
                return Failure(422, "INVALID_RESTART", "Vælg samme opgave og forsøgstype for at genstarte scenarierne.");
            scenarios = previous.Scenarios;
            request = request with { Intent = previous.InitialIntent };
        }
        else
        {
            scenarios = HomeRunCoordinator.Scenarios(task, request);
        }
        var run = await runs.Start(subject, request, state.Layout, program, scenarios, controller, IsTeacherSandbox, ct);
        if (run is null)
            return Failure(409, "RUN_BUSY", "Der kører allerede et forsøg, eller køen er fuld. Prøv igen om lidt.");
        return AcceptedAtAction(nameof(Status), new { id = run.Run.Id }, run);
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<HomeRunStatus>> Status(Guid id, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        var replay = await store.FindRun(subject, id, ct);
        return replay is null ? NotFound() : Ok(replay.Run);
    }

    [HttpPost("runs/{id:guid}/step")]
    [RequestSizeLimit(2_048)]
    public async Task<ActionResult<HomeRunStatus>> Step(Guid id, HomeStepRunRequest request, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        if (request.AfterSequence < 0 || request.MaxTicks is < 1 or > 20)
            return Failure(400, "INVALID_STEP", "Bed om mellem 1 og 20 simulationstrin.");
        var status = await runs.Step(subject, id, request);
        return status is null ? await Status(id, ct) : Ok(status);
    }

    [HttpPost("runs/{id:guid}/control")]
    [RequestSizeLimit(4_096)]
    public async Task<ActionResult<HomeRunStatus>> Control(Guid id, HomeRunControlRequest request, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        if (request.Action is not ("pause" or "resume" or "stop" or "input" or "speed")
            || request.Intent is { } intent && !KnownIntent(intent)
            || request.DaylightLux is < 0 or > 1_000 || request.Speed is not (null or 1 or 4 or 10))
            return Failure(400, "INVALID_CONTROL", "Vælg en gyldig handling til forsøget.");
        var status = await runs.Control(subject, id, request);
        return status is null ? await Status(id, ct) : Ok(status);
    }

    [HttpGet("runs/{id:guid}/replay")]
    public async Task<ActionResult<HomeRunReplay>> Replay(Guid id, CancellationToken ct)
    {
        if (Identify(out var subject) is { } rejection) return rejection;
        var replay = await store.FindRun(subject, id, ct);
        if (replay is null) return NotFound();
        if (replay.Run.Run.Status is "queued" or "compiling" or "running" or "paused")
            return Failure(409, "RUN_ACTIVE", "Afslut forsøget, før det kan genafspilles.");
        return Ok(replay);
    }

    private async Task<HomeGameState> Bootstrap(Guid subject, HomeProfileData state, CancellationToken ct,
        HomeClassroom[]? classrooms = null)
    {
        classrooms ??= await store.Classrooms(subject, ct);
        var last = await store.LatestRun(subject, ct);
        return new(state.Layout, await AvatarWallet.Gold(db, subject, ct), state.CompletedTasks, state.Drafts, state.Progress,
            state.Undo is not null, last?.Run.Run, await runner.IsAvailable(ct), AvailableTask(state, classrooms),
            state.Reflection, classrooms, IsTeacherSandbox);
    }

    private Task<HomeBuildReceiptRow?> Receipt(Guid subject, Guid id, CancellationToken ct) =>
        db.HomeGameBuildReceipts.AsNoTracking().SingleOrDefaultAsync(row => row.UserSub == subject && row.OperationId == id, ct);

    private async Task<ActionResult<HomeGameState>> Repeat(HomeBuildReceiptRow receipt, string fingerprint, CancellationToken ct)
    {
        if (receipt.RequestHash != fingerprint)
            return Failure(409, "OPERATION_REUSED", "Denne handling er allerede brugt til et andet køb eller en anden placering.");
        return Ok(await Bootstrap(receipt.UserSub, HomeGameJson.Read<HomeProfileData>(receipt.StateJson), ct));
    }

    private ActionResult? Identify(out Guid subject)
    {
        subject = UserClaims.GetSub(User) ?? Guid.Empty;
        if (!options.Value.Enabled) return NotFound();
        if (subject == Guid.Empty || User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (!Request.Headers.TryGetValue(SubjectHeader, out var expected)
            || !Guid.TryParse(expected.ToString(), out var supplied) || supplied != subject)
            return Failure(409, "ACCOUNT_CHANGED", "Kontosessionen er skiftet. Åbn Hjem.exe igen.");
        return null;
    }

    private static bool KnownIntent(string? intent) => intent is "read" or "relax" or "leave" or "idle" or "away";

    private static ObjectResult ConflictResult() =>
        Failure(409, "REVISION_CONFLICT", "Hjemmet er ændret et andet sted. Hent den seneste gemte version.");

    internal static ObjectResult Failure(int status, string code, string message, IReadOnlyList<HomeCell>? cells = null)
    {
        var problem = new ProblemDetails { Status = status, Detail = message };
        problem.Extensions["code"] = code;
        if (cells is { Count: > 0 }) problem.Extensions["cells"] = cells;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
