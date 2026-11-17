using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class OutlookEventDetailsTests
{
    private const string EventId = "AAMk/opaque+id=";

    [Fact]
    public async Task Uses_the_callers_mailbox_and_converts_a_full_html_description_to_text()
    {
        using var transport = new GraphTransport(Event());
        var details = await ReadDetails(transport);

        Assert.Equal("Fagteam", details.Event.Subject);
        Assert.Equal("Møderum 2", details.Event.Location);
        Assert.Equal("Idé & tid\nNæste skridt\nSlut", details.Description);
        Assert.Equal(new TeacherCalendarPerson("Arrangør", "organizer@example.test"), details.Organizer);
        Assert.Equal(new TeacherCalendarAttendee("Deltager", "attendee@example.test", "required", "accepted"),
            Assert.Single(details.Attendees));

        var uri = Assert.Single(transport.Requests);
        Assert.Equal("graph.microsoft.com", uri.Host);
        Assert.Equal("/v1.0/users/teacher%40example.test/events/AAMk%2Fopaque%2Bid%3D", uri.AbsolutePath);
        Assert.Contains("body,bodyPreview,organizer,attendees,hideAttendees,isOrganizer", Uri.UnescapeDataString(uri.Query));
    }

    [Theory]
    [InlineData("private")]
    [InlineData("Personal")]
    [InlineData("confidential")]
    public async Task Redacts_private_details_on_the_server(string sensitivity)
    {
        using var transport = new GraphTransport(Event(sensitivity: sensitivity));
        var details = await ReadDetails(transport);

        Assert.True(details.Event.IsPrivate);
        Assert.Equal("Privat", details.Event.Subject);
        Assert.Null(details.Event.Location);
        Assert.Null(details.Event.WebLink);
        Assert.Null(details.Event.OnlineMeetingUrl);
        Assert.Null(details.Description);
        Assert.Null(details.Organizer);
        Assert.Empty(details.Attendees);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Hides_attendees_from_recipients_when_requested(bool? isOrganizer, bool expectedHidden)
    {
        using var transport = new GraphTransport(Event(hideAttendees: true, isOrganizer: isOrganizer));
        var details = await ReadDetails(transport);

        Assert.Equal(expectedHidden, details.HideAttendees);
        Assert.Equal(expectedHidden ? 0 : 1, details.Attendees.Count);
        Assert.NotNull(details.Description);
    }

    [Fact]
    public async Task Students_cannot_fetch_event_details()
    {
        using var transport = new GraphTransport(Event());
        Assert.IsType<ForbidResult>(await Controller(transport, teacher: false).MineEvent(EventId, default));
        Assert.Empty(transport.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("event id?")]
    public async Task Invalid_ids_do_not_reach_the_provider(string? id)
    {
        using var transport = new GraphTransport(Event());
        Assert.IsType<BadRequestObjectResult>(await Controller(transport).MineEvent(id, default));
        Assert.Empty(transport.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, 404, "event_not_found")]
    [InlineData(HttpStatusCode.Forbidden, 502, "graph_unavailable")]
    public async Task Provider_errors_have_a_safe_public_response(HttpStatusCode providerStatus, int status, string code)
    {
        using var transport = new GraphTransport(new { error = "følsomt svar fra Graph" }, providerStatus);

        var result = Assert.IsAssignableFrom<ObjectResult>(await Controller(transport).MineEvent(EventId, default));
        Assert.Equal(status, result.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains(code, json);
        Assert.DoesNotContain("følsomt svar fra Graph", json);
    }

    private static async Task<TeacherCalendarEventDetails> ReadDetails(GraphTransport transport)
    {
        var result = Assert.IsType<OkObjectResult>(await Controller(transport).MineEvent(EventId, default));
        return Assert.IsType<TeacherCalendarEventDetails>(result.Value);
    }

    private static CalendarController Controller(GraphTransport transport, bool teacher = true)
    {
        var options = Options.Create(new AzureGraphOptions
        {
            TenantId = "fixture-tenant",
            ClientId = "fixture-client",
            ClientSecret = "fixture",
            TeacherUpnMap = "caller@example.test=teacher@example.test"
        });
        var claims = new Claim[]
        {
            new("sub", Guid.NewGuid().ToString()),
            new("email", "caller@example.test"),
            new(ClaimTypes.Role, teacher ? "Underviser" : "Elev")
        };
        var graph = new AzureGraphCalendarClient(transport, options, NullLogger<AzureGraphCalendarClient>.Instance);
        return new CalendarController(graph)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture")) }
            }
        };
    }

    private static object Event(string sensitivity = "normal", bool hideAttendees = false, bool? isOrganizer = true) => new
    {
        id = EventId,
        subject = "Fagteam",
        start = new { dateTime = "2026-11-16T10:00:00Z" },
        end = new { dateTime = "2026-11-16T11:00:00Z" },
        location = new { displayName = "Møderum 2" },
        sensitivity,
        isOnlineMeeting = true,
        webLink = "https://outlook.office.com/example",
        onlineMeeting = new { joinUrl = "https://teams.microsoft.com/example" },
        body = new { contentType = "html", content = "<p>Idé &amp; tid<br>Næste skridt</p><p hidden>skjult</p><style>p { color: red; }</style><div>Slut" },
        organizer = new { emailAddress = new { name = "Arrangør", address = "organizer@example.test" } },
        attendees = new[]
        {
            new { emailAddress = new { name = "Deltager", address = "attendee@example.test" }, type = "required", status = new { response = "accepted" } }
        },
        hideAttendees,
        isOrganizer
    };

    private sealed class GraphTransport(object payload, HttpStatusCode eventStatus = HttpStatusCode.OK)
        : HttpMessageHandler, IHttpClientFactory
    {
        public List<Uri> Requests { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { access_token = "fixture", expires_in = 3600 })
                });

            Assert.NotNull(request.RequestUri);
            Requests.Add(request.RequestUri);
            Assert.Contains("outlook.body-content-type=\"text\"", string.Join(", ", request.Headers.GetValues("Prefer")));
            return Task.FromResult(new HttpResponseMessage(eventStatus) { Content = JsonContent.Create(payload) });
        }
    }
}
