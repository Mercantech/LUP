using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/calendar")]
public sealed class CalendarController(AzureGraphCalendarClient graph) : ControllerBase
{
    /// <summary>Underviserens egen Outlook-kalender via Azure Graph (app-credentials).</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var sub = UserClaims.GetSub(User)?.ToString();
        var email = User.FindFirstValue("email");
        var upn = graph.ResolveTeacherUpn(sub, email);
        if (string.IsNullOrWhiteSpace(upn))
            return BadRequest(new { error = "missing_mailbox_mapping", message = "Din konto er ikke koblet til en Outlook-postkasse." });

        var start = from ?? StartOfWeek(DateTimeOffset.UtcNow);
        var end = to ?? start.AddDays(7);
        if (end <= start)
            return BadRequest(new { error = "invalid_range" });
        if (end - start > TimeSpan.FromDays(45))
            return BadRequest(new { error = "range_too_large", message = "Højst 45 dage ad gangen." });

        if (!graph.IsConfigured)
        {
            return Ok(new TeacherCalendarView(upn, start, end, Configured: false, Events: []));
        }

        try
        {
            var events = await graph.GetCalendarViewAsync(upn, start, end, ct);
            var visible = TeacherCalendarHoldFilter.HideDuplicateHolds(
                events.Where(e => !e.IsCancelled).ToList());
            return Ok(new TeacherCalendarView(upn, start, end, Configured: true, Events: visible));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new { error = "mailbox_not_found", message = $"Ingen postkasse for {upn}." });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "graph_unavailable",
                message = "Azure-kalenderen kunne ikke hentes lige nu.",
                detail = ex.Message
            });
        }
    }

    [HttpGet("mine/event")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MineEvent([FromQuery] string? id, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        // Outlook event IDs use opaque Base64 text. Keep the ID in one Graph path segment.
        if (id is not { Length: > 0 and <= 2048 } ||
            id.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not ('+' or '/' or '=' or '-' or '_')))
            return BadRequest(new { error = "invalid_event_id", message = "Aftalens id er ugyldigt." });

        var upn = graph.ResolveTeacherUpn(UserClaims.GetSub(User)?.ToString(), User.FindFirstValue("email"));
        if (string.IsNullOrWhiteSpace(upn))
            return BadRequest(new { error = "missing_mailbox_mapping", message = "Din konto er ikke koblet til en Outlook-postkasse." });

        if (!graph.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "graph_not_configured", message = "Outlook-integrationen er ikke tilgængelig." });

        try
        {
            return Ok(await graph.GetEventAsync(upn, id, ct));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return NotFound(new { error = "event_not_found", message = "Aftalen findes ikke længere i din Outlook-kalender." });
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or ArgumentException
                                   || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "graph_unavailable", message = "Aftalen kunne ikke hentes fra Outlook." });
        }
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset utc)
    {
        var localDate = DateOnly.FromDateTime(utc.UtcDateTime);
        var monday = localDate.AddDays(-((int)localDate.DayOfWeek + 6) % 7);
        return new DateTimeOffset(monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}
