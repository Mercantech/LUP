using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class HoldCalendarOutlookTests : IAsyncLifetime
{
    private const string Kasper = "kasper@example.invalid";
    private const string Mathias = "mathias@example.invalid";
    private readonly Guid teacher = Guid.NewGuid(), otherTeacher = Guid.NewGuid(), student = Guid.NewGuid(), unmapped = Guid.NewGuid();
    private readonly Guid holdId = Guid.NewGuid(), eventId = Guid.NewGuid();
    private readonly string path = Path.Combine(Path.GetTempPath(), $"hold-outlook-tests-{Guid.NewGuid():N}.db");
    private readonly FakeGraph transport = new();

    public async Task InitializeAsync()
    {
        await using var db = Open();
        await db.Database.EnsureCreatedAsync();
        db.Hold.Add(new()
        {
            Id = holdId, Name = "Testhold",
            Forloeb = new() { Id = Guid.NewGuid(), Name = "Programmering", CreatedBySub = teacher }
        });
        db.HoldCalendarEvents.Add(new()
        {
            Id = eventId, HoldId = holdId, Title = "Syntetisk workshop", Description = "Fiktivt indhold", Location = "Prøvelokale",
            StartUtc = new(2026, 11, 23, 8, 0, 0, TimeSpan.Zero), EndUtc = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        transport.Dispose();
        File.Delete(path);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Only_mapped_hold_managers_can_choose_the_two_target_ids()
    {
        await using var db = Open();
        var owner = As(db, teacher);
        Assert.IsType<ForbidResult>(await As(db, student, teacherRole: false).CopyToOutlook(holdId, eventId, new(["kasper"]), default));
        Assert.IsType<ForbidResult>(await As(db, unmapped, email: "other@example.invalid").OutlookCopies(holdId, eventId, default));
        Assert.IsType<NotFoundResult>(await As(db, otherTeacher).CopyToOutlook(holdId, eventId, new(["kasper"]), default));
        Assert.IsType<NotFoundResult>(await owner.OutlookCopies(holdId, Guid.NewGuid(), default));
        Assert.IsType<BadRequestObjectResult>(await owner.CopyToOutlook(holdId, eventId, new(["kasper", "other@example.invalid"]), default));
        Assert.IsType<BadRequestObjectResult>(await owner.CopyToOutlook(holdId, eventId, new([]), default));

        Assert.Equal(new OutlookCalendarCopyView[] { new("kasper", "pending"), new("mathias", "pending") },
            Copies(await owner.OutlookCopies(holdId, eventId, default)));
        Assert.Empty(transport.Posted);
        Assert.Empty(await db.HoldCalendarOutlookCopies.ToListAsync());
    }

    [Fact]
    public async Task A_partial_copy_retries_only_the_failed_destination_with_the_same_transaction_id()
    {
        var mathiasAttempts = 0;
        transport.Respond = (request, _) => Task.FromResult(request.Mailbox == Mathias && Interlocked.Increment(ref mathiasAttempts) == 1
            ? new HttpResponseMessage(HttpStatusCode.Forbidden) : Created(request.Mailbox));
        await using var db = Open();
        var owner = As(db, teacher);

        Assert.Equal(new OutlookCalendarCopyView[]
        {
            new("kasper", "copied"), new("mathias", "failed", "outlook_permission_denied")
        }, Copies(await owner.CopyToOutlook(holdId, eventId, new(["kasper", "mathias", "kasper"]), default)));
        Assert.Equal("Syntetisk workshop", (await db.HoldCalendarEvents.SingleAsync()).Title);
        Assert.Equal(2, transport.Posted.Count);
        Assert.Equal(new OutlookCalendarCopyView[]
        {
            new("kasper", "copied"), new("mathias", "failed", "outlook_permission_denied")
        }, Copies(await owner.OutlookCopies(holdId, eventId, default)));

        Assert.All(Copies(await owner.CopyToOutlook(holdId, eventId, new(["mathias", "kasper"]), default)),
            item => Assert.Equal("copied", item.Status));
        Assert.All(Copies(await owner.CopyToOutlook(holdId, eventId, new(["kasper", "mathias"]), default)),
            item => Assert.Equal("copied", item.Status));

        var byMailbox = transport.Posted.ToLookup(item => item.Mailbox);
        Assert.Single(byMailbox[Kasper]);
        Assert.Equal(2, byMailbox[Mathias].Count());
        Assert.Single(byMailbox[Mathias].Select(item => item.TransactionId).Distinct());
        Assert.NotEqual(byMailbox[Kasper].Single().TransactionId, byMailbox[Mathias].First().TransactionId);

        var receipts = await db.HoldCalendarOutlookCopies.AsNoTracking().OrderBy(row => row.Mailbox).ToArrayAsync();
        Assert.Equal(new[] { Kasper, Mathias }, receipts.Select(row => row.Mailbox));
        Assert.Equal(new[] { "id-kasper", "id-mathias" }, receipts.Select(row => row.GraphEventId));
        Assert.All(receipts, row => { Assert.Equal(eventId, row.EventId); Assert.Null(row.AttemptId); });

        Assert.IsType<OkObjectResult>(await owner.Delete(holdId, eventId, 0, default));
        Assert.Empty(await db.HoldCalendarOutlookCopies.ToListAsync());
        Assert.Equal(3, transport.Posted.Count);
    }

    [Fact]
    public async Task Simultaneous_requests_cannot_both_post_the_same_event_to_the_same_mailbox()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.Respond = async (request, ct) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(ct);
            return Created(request.Mailbox);
        };
        await using var firstDb = Open();
        await using var secondDb = Open();
        var owner = As(firstDb, teacher);
        var concurrent = As(secondDb, teacher);
        var first = owner.CopyToOutlook(holdId, eventId, new(["kasper"]), default);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new OutlookCalendarCopyView("kasper", "failed", "outlook_copy_in_progress"),
                Assert.Single(Copies(await concurrent.CopyToOutlook(holdId, eventId, new(["kasper"]), default))));
            Assert.Single(transport.Posted);
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.Equal(new OutlookCalendarCopyView("kasper", "copied"), Assert.Single(Copies(await first)));
        Assert.Equal("id-kasper", (await secondDb.HoldCalendarOutlookCopies.AsNoTracking().SingleAsync()).GraphEventId);
    }

    [Fact]
    public async Task Unconfigured_outlook_does_not_change_the_saved_lup_appointment()
    {
        await using var db = Open();
        var result = await As(db, teacher, configured: false).CopyToOutlook(holdId, eventId, new(["kasper"]), default);
        Assert.Equal(new OutlookCalendarCopyView("kasper", "failed", "outlook_not_configured"), Assert.Single(Copies(result)));
        Assert.Equal(eventId, (await db.HoldCalendarEvents.SingleAsync()).Id);
        Assert.Empty(await db.HoldCalendarOutlookCopies.ToListAsync());
        Assert.Empty(transport.Posted);
    }

    private AppDbContext Open() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);

    private HoldCalendarController As(AppDbContext db, Guid sub, bool teacherRole = true, bool configured = true, string? email = null)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) };
        if (teacherRole) claims.Add(new(ClaimTypes.Role, "Underviser"));
        if (email is not null) claims.Add(new("email", email));
        var graph = new AzureGraphCalendarClient(new HttpFactory(transport), Options.Create(new AzureGraphOptions
        {
            TenantId = configured ? "tenant-placeholder" : "", ClientId = "client-placeholder", ClientSecret = "secret-placeholder",
            TeacherUpnMap = $"{teacher:D}={Kasper};{otherTeacher:D}={Mathias}",
            CalendarWriteTargets = $"kasper={Kasper};mathias={Mathias}"
        }), NullLogger<AzureGraphCalendarClient>.Instance);
        return new(db, new(db), graph)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } }
        };
    }

    private static OutlookCalendarCopyView[] Copies(IActionResult result) =>
        Assert.IsType<OutlookCalendarCopies>(Assert.IsType<OkObjectResult>(result).Value).Copies;

    private static HttpResponseMessage Created(string mailbox) =>
        new(HttpStatusCode.Created) { Content = JsonContent.Create(new { id = mailbox == Kasper ? "id-kasper" : "id-mathias" }) };

    private sealed class FakeGraph : HttpMessageHandler
    {
        public ConcurrentQueue<PostedEvent> Posted { get; } = new();
        public Func<PostedEvent, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (request, _) => Task.FromResult(Created(request.Mailbox));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri?.Host == "login.microsoftonline.com")
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "fake-token", expires_in = 3600 }) };

            Assert.Equal("graph.microsoft.com", request.RequestUri?.Host);
            Assert.Equal(HttpMethod.Post, request.Method);
            var mailbox = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Split('/')[3]);
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
            var copied = new PostedEvent(mailbox, body.GetProperty("transactionId").GetString()!);
            Assert.Equal("Syntetisk workshop", body.GetProperty("subject").GetString());
            Posted.Enqueue(copied);
            return await Respond(copied, ct);
        }
    }

    private sealed record PostedEvent(string Mailbox, string TransactionId);
    private sealed class HttpFactory(FakeGraph transport) : IHttpClientFactory
    {
        public HttpClient CreateClient(string _) => new(transport, disposeHandler: false);
    }
}
