using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class CalendarControllerTests
{
    [Fact]
    public async Task StudentsAreForbidden()
    {
        var controller = Controller(email: "elev@mercantec.dk", teacher: false);
        Assert.IsType<ForbidResult>(await MineWithoutDatabase(controller));
    }

    [Fact]
    public async Task TeacherWithoutMailboxMappingGetsBadRequest()
    {
        var controller = Controller(email: null, teacher: true, sub: Guid.NewGuid());
        var result = Assert.IsType<BadRequestObjectResult>(await MineWithoutDatabase(controller));
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("missing_mailbox_mapping", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MathiasSubMapsToMagsMailboxWhenUnconfigured()
    {
        var mathias = Guid.Parse("d969fced-f7be-46d4-9bb9-5a2f03ca6974");
        var controller = Controller(email: "mathiasgsteenberg@gmail.com", teacher: true, configured: false, sub: mathias);
        var ok = Assert.IsType<OkObjectResult>(await MineWithoutDatabase(controller));
        var view = Assert.IsType<TeacherCalendarView>(ok.Value);
        Assert.False(view.Configured);
        Assert.Equal("mags@mercantec.dk", view.AzureUpn);
        Assert.Empty(view.Events);
    }

    [Fact]
    public async Task UnconfiguredGraphReturnsEmptyConfiguredFalse()
    {
        var controller = Controller(email: "kasc@mercantec.dk", teacher: true, configured: false);
        var ok = Assert.IsType<OkObjectResult>(await MineWithoutDatabase(controller));
        var view = Assert.IsType<TeacherCalendarView>(ok.Value);
        Assert.False(view.Configured);
        Assert.Equal("kasc@mercantec.dk", view.AzureUpn);
        Assert.Empty(view.Events);
    }

    [Fact]
    public void OutlookTargetsOnlyExposeLabelsForExplicitlyMappedTeachers()
    {
        var student = Controller(email: "kasc@mercantec.dk", teacher: false, configured: true);
        Assert.IsType<ForbidResult>(student.OutlookTargets());

        var valid = Controller(email: "kasc@mercantec.dk", teacher: true, configured: true);
        var available = Assert.IsType<OutlookCalendarTargets>(Assert.IsType<OkObjectResult>(valid.OutlookTargets()).Value);
        Assert.True(available.Configured);
        Assert.Equal(new OutlookCalendarTarget[] { new("kasper", "Kasper"), new("mathias", "Mathias") }, available.Targets);
        Assert.DoesNotContain("@", JsonSerializer.Serialize(available), StringComparison.Ordinal);

        var disabled = Controller(email: "kasc@mercantec.dk", teacher: true);
        var notConfigured = Assert.IsType<OutlookCalendarTargets>(Assert.IsType<OkObjectResult>(disabled.OutlookTargets()).Value);
        Assert.False(notConfigured.Configured);
        Assert.Equal(available.Targets, notConfigured.Targets);

        var fallback = Controller(email: "kasc@mercantec.dk", teacher: true, configured: true, upnMap: "");
        var unavailable = Assert.IsType<OutlookCalendarTargets>(Assert.IsType<OkObjectResult>(fallback.OutlookTargets()).Value);
        Assert.False(unavailable.Configured);
        Assert.Empty(unavailable.Targets);
    }

    [Fact]
    public async Task GraphWritesOnlyToAllowlistedCalendarAndSendsNoAttendees()
    {
        var order = new List<string>();
        using var transport = new Transport(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            if (request.RequestUri!.Host == "login.microsoftonline.com")
            {
                order.Add("token");
                return TokenResponse();
            }

            order.Add("calendar");
            Assert.Equal("graph.microsoft.com", request.RequestUri.Host);
            Assert.Equal("/v1.0/users/kasc@mercantec.dk/calendar/events",
                Uri.UnescapeDataString(request.RequestUri.AbsolutePath));
            Assert.Equal("fixture-token", request.Headers.Authorization?.Parameter);
            using var json = await request.Content!.ReadFromJsonAsync<JsonDocument>(ct);
            var payload = json!.RootElement;
            Assert.Equal("Holdaktivitet", payload.GetProperty("subject").GetString());
            Assert.Equal("<b>Præcis beskrivelse</b>", payload.GetProperty("body").GetProperty("content").GetString());
            Assert.Equal("text", payload.GetProperty("body").GetProperty("contentType").GetString());
            Assert.Equal("2027-05-10T07:15:00.0000000", payload.GetProperty("start").GetProperty("dateTime").GetString());
            Assert.Equal("2027-05-10T08:15:00.0000000", payload.GetProperty("end").GetProperty("dateTime").GetString());
            Assert.Equal("UTC", payload.GetProperty("start").GetProperty("timeZone").GetString());
            Assert.Equal("UTC", payload.GetProperty("end").GetProperty("timeZone").GetString());
            Assert.Equal("A42", payload.GetProperty("location").GetProperty("displayName").GetString());
            Assert.Equal("fixture-transaction", payload.GetProperty("transactionId").GetString());
            Assert.False(payload.TryGetProperty("attendees", out _));
            Assert.False(payload.TryGetProperty("isOnlineMeeting", out _));
            return new(HttpStatusCode.Created) { Content = JsonContent.Create(new { id = "opaque+/fixture=" }) };
        });
        var graph = CreateGraph(true, transport);
        var start = new DateTimeOffset(2027, 5, 10, 9, 15, 0, TimeSpan.FromHours(2));
        var activity = new HoldCalendarEventView(Guid.NewGuid(), Guid.NewGuid(), "Holdaktivitet", "<b>Præcis beskrivelse</b>",
            start, start.AddHours(1), "A42", "teaching", 1);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            graph.CreateCalendarEventAsync("anden@mercantec.dk", activity, "fixture-transaction", default));
        Assert.Empty(order);
        var outlookId = await graph.CreateCalendarEventAsync("kasc@mercantec.dk", activity, "fixture-transaction", default);
        Assert.Equal("opaque+/fixture=", outlookId);
        Assert.Equal(new[] { "token", "calendar" }, order);
    }

    [Fact]
    public async Task OutlookViewLinksOnlyExactIdsInTheCurrentMailbox()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var holdId = Guid.NewGuid();
        var start = new DateTimeOffset(2027, 5, 10, 7, 15, 0, TimeSpan.Zero);
        db.Hold.Add(new() { Id = holdId, Forloeb = new() { Id = Guid.NewGuid(), Name = "QA Hold" }, Name = "QA Hold" });
        var eventId = Guid.NewGuid();
        db.HoldCalendarEvents.Add(new()
        {
            Id = eventId, HoldId = holdId, Title = "Holdaktivitet", StartUtc = start, EndUtc = start.AddHours(1)
        });
        db.HoldCalendarOutlookCopies.AddRange(
            new() { EventId = eventId, Mailbox = "kasc@mercantec.dk", TargetId = "kasper", GraphEventId = "opaque+exact=" },
            new() { EventId = eventId, Mailbox = "mags@mercantec.dk", TargetId = "mathias", GraphEventId = "otherMailbox=" });
        await db.SaveChangesAsync();
        using var transport = new Transport((request, _) => Task.FromResult(request.RequestUri!.Host == "login.microsoftonline.com"
            ? TokenResponse()
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    value = new[] { "opaque+exact=", "opaque+Exact=", "otherMailbox=", "unlinked" }.Select(id => new
                    {
                        id,
                        subject = "Holdaktivitet",
                        start = new { dateTime = start.ToString("O"), timeZone = "UTC" },
                        end = new { dateTime = start.AddHours(1).ToString("O"), timeZone = "UTC" }
                    })
                })
            }));
        var controller = Controller(email: "KASC@mercantec.dk", teacher: true, configured: true, factory: transport);
        var ok = Assert.IsType<OkObjectResult>(await controller.Mine(start, start.AddDays(1), db, default));
        var view = Assert.IsType<TeacherCalendarView>(ok.Value);
        Assert.Collection(view.Events,
            linked =>
            {
                Assert.Equal("opaque+exact=", linked.Id);
                Assert.Equal(eventId, linked.LupEventId);
            },
            differentCase => Assert.Null(differentCase.LupEventId),
            differentMailbox => Assert.Null(differentMailbox.LupEventId),
            unlinked => Assert.Null(unlinked.LupEventId));
    }

    private static async Task<IActionResult> MineWithoutDatabase(CalendarController controller)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        return await controller.Mine(null, null, db, default);
    }

    private static AzureGraphCalendarClient CreateGraph(bool configured, IHttpClientFactory? factory, string? upnMap = null)
    {
        var opts = new AzureGraphOptions
        {
            TenantId = configured ? "t" : "",
            ClientId = configured ? "c" : "",
            ClientSecret = configured ? "s" : ""
        };
        if (upnMap is not null) opts.TeacherUpnMap = upnMap;
        return new(factory ?? new EmptyHttpClientFactory(), Options.Create(opts), NullLogger<AzureGraphCalendarClient>.Instance);
    }

    private static CalendarController Controller(string? email, bool teacher, bool configured = false, Guid? sub = null,
        IHttpClientFactory? factory = null, string? upnMap = null)
    {
        var claims = new List<Claim>();
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        // Admin tæller også som underviser i UserClaims
        if (teacher) claims.Add(new("role", "Admin"));
        if (email is not null) claims.Add(new("email", email));
        claims.Add(new("sub", (sub ?? Guid.NewGuid()).ToString()));

        return new CalendarController(CreateGraph(configured, factory, upnMap))
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    private sealed class EmptyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static HttpResponseMessage TokenResponse() =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "fixture-token", expires_in = 3600 }) };

    private sealed class Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
