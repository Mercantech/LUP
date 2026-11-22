using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Route("api/extension/calendar"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(AuthenticationSchemes = ExtensionUploadCredentials.Scheme)]
public sealed class ExtensionCalendarController(AppDbContext db, CourseAccess access, AzureGraphCalendarClient graph,
    ExtensionUploadRateLimit rate) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        if (!User.HasClaim(ExtensionUploadCredentials.ScopeClaim, ExtensionUploadCredentials.CalendarScope))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "extension_calendar_not_enabled" });
        if (UserClaims.GetSub(User) is not Guid viewer || viewer == Guid.Empty || !UserClaims.IsTeacher(User)
            || !Guid.TryParse(User.FindFirstValue(ExtensionUploadCredentials.KeyIdClaim), out var keyId))
            return Forbid(ExtensionUploadCredentials.Scheme);
        if (from is not { } start || to is not { } end || end <= start || end - start > TimeSpan.FromDays(8))
            return BadRequest(new { error = "invalid_range" });
        if (!rate.TryAcquire(keyId, ExtensionUploadCredentials.CalendarScope))
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "extension_rate_limit" });
        }

        var plan = await new HoldCalendarAccess(db).ReadOverviewAsync(access.AccessibleHolds(User),
            access.AccessibleHolds(User, manageOnly: true), viewer, start.ToUniversalTime(), end.ToUniversalTime(), ct,
            includeQuestChoices: false);
        var holdNames = plan.Holds.ToDictionary(hold => hold.HoldId, hold => hold.Name);
        var agenda = plan.Events.Select(item => new ExtensionCalendarItem($"hold:{item.Id:D}", item.Title,
            item.StartUtc, item.EndUtc, false, "teaching", holdNames[item.HoldId], item.Location))
            .Concat(plan.Deadlines.Select(item => new ExtensionCalendarItem($"quest:{item.QuestId:D}", item.Title,
                item.DeadlineUtc, item.DeadlineUtc, false, "deadline", holdNames[item.HoldId], null)))
            .ToList();

        var outlookStatus = await AddOutlookAsync(agenda, plan.Events, start, end, viewer, ct);
        return Ok(new ExtensionCalendarView(agenda.OrderBy(item => item.Start).ThenBy(item => item.Id).ToArray(), outlookStatus));
    }

    private async Task<string> AddOutlookAsync(List<ExtensionCalendarItem> agenda, HoldCalendarEventView[] holdEvents,
        DateTimeOffset start, DateTimeOffset end, Guid viewer, CancellationToken ct)
    {
        var upn = graph.ResolveMappedTeacherUpn(viewer.ToString("D"), User.FindFirstValue("email"));
        if (!graph.IsConfigured || string.IsNullOrWhiteSpace(upn)) return "not_connected";

        using var lookup = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lookup.CancelAfter(TimeSpan.FromSeconds(10));
        IReadOnlyList<TeacherCalendarEvent> events;
        try
        {
            events = await CalendarController.ReadOutlookAsync(graph, db, upn, start, end, lookup.Token);
        }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            return "not_connected";
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or FormatException or ArgumentException
                                      || error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return "unavailable";
        }

        var holds = holdEvents.ToDictionary(item => item.Id);
        foreach (var item in events)
        {
            if (!item.IsAllDay && item.LupEventId is Guid id && holds.TryGetValue(id, out var hold)
                && item.Start == hold.StartUtc && item.End == hold.EndUtc) continue;
            agenda.Add(new($"outlook:{item.Id}", item.IsPrivate ? "Optaget" : item.Subject,
                item.Start, item.End, item.IsAllDay, "outlook", "Outlook", item.IsPrivate ? null : item.Location));
        }
        return "ready";
    }
}

public sealed record ExtensionCalendarView(ExtensionCalendarItem[] Events, string OutlookStatus);
public sealed record ExtensionCalendarItem(string Id, string Title, DateTimeOffset Start, DateTimeOffset End,
    bool IsAllDay, string Kind, string Source, string? Location);
