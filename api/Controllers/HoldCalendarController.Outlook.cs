using System.Data.Common;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class HoldCalendarController
{
    private static readonly TimeSpan CopyTimeBudget = TimeSpan.FromSeconds(18);
    private static readonly TimeSpan ReceiptTimeBudget = TimeSpan.FromSeconds(5);
    private const int CopyLeaseSeconds = 45;

    [HttpGet("{id:guid}/outlook")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> OutlookCopies(Guid holdId, Guid id, CancellationToken ct)
    {
        var denied = ReadOutlookTargets(out var targets);
        if (denied is not null) return denied;
        if (!await access.CanManageAsync(holdId, User, ct)
            || !await db.HoldCalendarEvents.AnyAsync(item => item.Id == id && item.HoldId == holdId, ct))
            return NotFound();

        var receipts = await OutlookReceipts(id, targets.Values.ToArray(), ct);
        return Ok(new OutlookCalendarCopies(targets.Select(target => CopyStatus(target.Key, receipts.GetValueOrDefault(target.Value),
            pending: true)).ToArray()));
    }

    [HttpPost("{id:guid}/outlook")]
    [RequestSizeLimit(4096)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> CopyToOutlook(Guid holdId, Guid id, CopyHoldCalendarEventToOutlook request, CancellationToken ct)
    {
        var denied = ReadOutlookTargets(out var targets);
        if (denied is not null) return denied;
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        if (request.TargetIds is not { Length: > 0 }
            || request.TargetIds.Any(target => target is not ("kasper" or "mathias") || !targets.ContainsKey(target)))
            return BadRequest(new { error = "invalid_outlook_targets" });

        var source = await (from item in db.HoldCalendarEvents.AsNoTracking()
                            join hold in db.Hold on item.HoldId equals hold.Id
                            where item.Id == id && item.HoldId == holdId
                            select new { Event = item, HoldName = hold.Name }).SingleOrDefaultAsync(ct);
        if (source is null) return NotFound();

        var wanted = request.TargetIds.Distinct(StringComparer.Ordinal).ToArray();
        var receipts = await OutlookReceipts(id, wanted.Select(target => targets[target]).ToArray(), ct);
        var results = new OutlookCalendarCopyView[wanted.Length];
        var attempts = new List<OutlookAttempt>();
        var configured = graph?.IsConfigured == true;
        for (var i = 0; i < wanted.Length; i++)
        {
            var target = wanted[i];
            var mailbox = targets[target];
            if (receipts.GetValueOrDefault(mailbox)?.GraphEventId is not null)
                results[i] = new(target, "copied");
            else if (!configured)
                results[i] = new(target, "failed", "outlook_not_configured");
            else
            {
                var attempt = new OutlookAttempt(i, target, mailbox, Guid.NewGuid());
                try
                {
                    if (await TryLeaseAsync(holdId, id, attempt, ct))
                        attempts.Add(attempt);
                    else
                    {
                        var current = await db.HoldCalendarOutlookCopies.AsNoTracking()
                            .SingleOrDefaultAsync(row => row.EventId == id && row.Mailbox == mailbox, ct);
                        results[i] = CopyStatus(target, current, pending: false);
                    }
                }
                catch (DbException) when (!ct.IsCancellationRequested)
                {
                    results[i] = new(target, "failed", "outlook_unavailable");
                }
            }
        }

        if (attempts.Count > 0)
        {
            using var graphBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            graphBudget.CancelAfter(CopyTimeBudget);
            var appointment = View(source.Event);
            var outcomes = await Task.WhenAll(attempts.Select(attempt => SendToOutlook(appointment, source.HoldName, attempt, graphBudget.Token)));

            // Save acknowledgements even if the browser disconnected after Outlook created its copy.
            using var receiptBudget = new CancellationTokenSource(ReceiptTimeBudget);
            foreach (var outcome in outcomes)
                results[outcome.Attempt.Index] = await ConfirmOutcomeAsync(id, outcome, receiptBudget.Token);
        }

        ct.ThrowIfCancellationRequested();
        return Ok(new OutlookCalendarCopies(results));
    }

    private IActionResult? ReadOutlookTargets(out IReadOnlyDictionary<string, string> targets)
    {
        targets = new Dictionary<string, string>();
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var allowed = graph?.GetCalendarCopyMailboxes(subject.ToString(), User.FindFirstValue("email"));
        if (allowed is not { Count: > 0 }) return Forbid();
        targets = allowed.Where(item => item.Key is "kasper" or "mathias")
            .ToDictionary(item => item.Key, item => item.Value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
        return targets.Count == 0 ? Forbid() : null;
    }

    private Task<Dictionary<string, HoldCalendarOutlookCopy>> OutlookReceipts(Guid id, string[] mailboxes, CancellationToken ct) =>
        db.HoldCalendarOutlookCopies.AsNoTracking().Where(row => row.EventId == id && mailboxes.Contains(row.Mailbox))
            .ToDictionaryAsync(row => row.Mailbox, ct);

    private static OutlookCalendarCopyView CopyStatus(string target, HoldCalendarOutlookCopy? receipt, bool pending)
    {
        if (receipt?.GraphEventId is not null) return new(target, "copied");
        if (receipt?.ErrorCode is { } error) return new(target, "failed", error);
        if (receipt?.AttemptId is not null && receipt.LeaseExpiresUnixSeconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return new(target, "failed", "outlook_timeout");
        return pending ? new(target, "pending") : new(target, "failed", "outlook_copy_in_progress");
    }

    private async Task<bool> TryLeaseAsync(Guid holdId, Guid eventId, OutlookAttempt attempt, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // One writer can contact Graph for each event/mailbox pair. Timed-out or interrupted
        // attempts may be retried using the same Graph transaction ID.
        var leased = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO hold_calendar_outlook_copies ("EventId", "Mailbox", "TargetId", "AttemptId", "LeaseExpiresUnixSeconds")
            SELECT {eventId}, {attempt.Mailbox}, {attempt.TargetId}, {attempt.Token}, {now + CopyLeaseSeconds}
            WHERE EXISTS (
                SELECT 1 FROM hold_calendar_events WHERE "Id" = {eventId} AND "HoldId" = {holdId}
            )
            ON CONFLICT ("EventId", "Mailbox") DO UPDATE SET
                "TargetId" = excluded."TargetId",
                "ErrorCode" = NULL,
                "AttemptId" = excluded."AttemptId",
                "LeaseExpiresUnixSeconds" = excluded."LeaseExpiresUnixSeconds"
            WHERE hold_calendar_outlook_copies."GraphEventId" IS NULL
                AND (hold_calendar_outlook_copies."AttemptId" IS NULL
                    OR hold_calendar_outlook_copies."LeaseExpiresUnixSeconds" <= {now})
            """, ct);
        return leased == 1;
    }

    private async Task<OutlookOutcome> SendToOutlook(HoldCalendarEventView appointment, string holdName, OutlookAttempt attempt,
        CancellationToken ct)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{appointment.Id:D}\n{attempt.Mailbox}"));
        var transactionId = new Guid(bytes.AsSpan(0, 16)).ToString("D");
        try
        {
            var graphId = await graph!.CreateCalendarEventAsync(attempt.Mailbox, appointment, holdName, transactionId, ct);
            return graphId is { Length: > 0 and <= 2048 } && !string.IsNullOrWhiteSpace(graphId) && !graphId.Any(char.IsControl)
                ? new(attempt, graphId, null) : new(attempt, null, "outlook_unavailable");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new(attempt, null, "outlook_timeout");
        }
        catch (HttpRequestException ex)
        {
            var code = ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "outlook_permission_denied",
                HttpStatusCode.NotFound => "outlook_mailbox_not_found",
                HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => "outlook_timeout",
                _ => "outlook_unavailable"
            };
            return new(attempt, null, code);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or OperationCanceledException)
        {
            return new(attempt, null, "outlook_unavailable");
        }
    }

    private async Task<OutlookCalendarCopyView> ConfirmOutcomeAsync(Guid id, OutlookOutcome outcome, CancellationToken ct)
    {
        var target = outcome.Attempt.TargetId;
        try
        {
            var saved = await db.HoldCalendarOutlookCopies.Where(row => row.EventId == id && row.Mailbox == outcome.Attempt.Mailbox
                && row.AttemptId == outcome.Attempt.Token).ExecuteUpdateAsync(update => update
                    .SetProperty(row => row.GraphEventId, outcome.GraphEventId)
                    .SetProperty(row => row.ErrorCode, outcome.ErrorCode)
                    .SetProperty(row => row.AttemptId, (Guid?)null)
                    .SetProperty(row => row.LeaseExpiresUnixSeconds, 0L), ct);
            if (saved == 1)
                return outcome.GraphEventId is null ? new(target, "failed", outcome.ErrorCode) : new(target, "copied");
        }
        catch (Exception ex) when (ex is DbException or OperationCanceledException)
        {
            // Outlook may already have accepted the request. A retry reuses its transaction ID.
        }

        return new(target, "failed", outcome.ErrorCode ?? "outlook_unconfirmed");
    }

    private sealed record OutlookAttempt(int Index, string TargetId, string Mailbox, Guid Token);
    private sealed record OutlookOutcome(OutlookAttempt Attempt, string? GraphEventId, string? ErrorCode);
}
