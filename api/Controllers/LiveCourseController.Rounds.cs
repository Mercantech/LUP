using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class LiveCourseController
{
    private const int MaxAnswersPerParticipant = 8;
    private const int MaxEntriesPerRound = 1000;

    [HttpPost("{sessionId:guid}/rounds")]
    public Task<ActionResult<LiveCourseView>> OpenRound(Guid pageId, Guid sessionId, OpenLiveCourseRound request, CancellationToken ct) =>
        Change(pageId, sessionId, true, async (session, _, now) =>
        {
            var bindings = JsonSerializer.Deserialize<LiveCourseBinding[]>(session.BindingsJson) ?? [];
            if (!ValidBlockId(request.BlockId) || !bindings.Any(b => b.BlockId == request.BlockId && b.ModuleId == request.ModuleId
                && b.SlideIndex == session.SlideIndex)) return NotFound();
            var module = await db.LiveCourseModules.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.ModuleId
                && m.PageId == pageId && m.BlockId == request.BlockId, ct);
            if (module is null) return NotFound();
            var current = await ActiveRound(session, session.ActiveRoundId, ct);
            if (current?.ModuleId == module.Id && IsOpen(current, now)) return null;
            if (await db.LiveCourseRounds.CountAsync(r => r.SessionId == session.Id, ct) >= MaxRoundsPerSession)
                return Conflict(new { error = "round_limit" });
            await CloseRound(session, now, ct);
            var round = new LiveCourseRound
            {
                Id = Guid.NewGuid(), SessionId = sessionId, ModuleId = module.Id, OpenedAtUtc = now,
                ShowResults = module.Kind == "word-cloud",
                DeadlineUtc = module.DurationSeconds is int seconds ? now.AddSeconds(seconds) : null
            };
            db.LiveCourseRounds.Add(round);
            session.ActiveRoundId = round.Id;
            return null;
        }, ct);

    [HttpPost("{sessionId:guid}/rounds/{roundId:guid}/control")]
    public Task<ActionResult<LiveCourseView>> ControlRound(Guid pageId, Guid sessionId, Guid roundId,
        ControlLiveCourseRound request, CancellationToken ct) =>
        Change(pageId, sessionId, true, async (session, _, now) =>
        {
            var round = await ActiveRound(session, roundId, ct);
            if (round is null) return NotFound();
            switch (request.Action)
            {
                case "close":
                    round.ClosedAtUtc ??= now;
                    break;
                case "show":
                    if (round.Module.Kind == "quiz") round.ClosedAtUtc ??= now;
                    round.ShowResults = true;
                    break;
                case "hide":
                    round.ShowResults = false;
                    break;
                default:
                    return BadRequest(new { error = "invalid_round_action" });
            }
            return null;
        }, ct);

    [HttpPost("{sessionId:guid}/rounds/{roundId:guid}/answer")]
    public Task<ActionResult<LiveCourseView>> Answer(Guid pageId, Guid sessionId, Guid roundId,
        AnswerLiveCourseRound request, CancellationToken ct) =>
        Change(pageId, sessionId, false, async (session, access, now) =>
        {
            var round = await ActiveRound(session, roundId, ct);
            if (round is null || !IsOpen(round, now)) return Conflict(new { error = "round_closed" });
            var text = request.Text?.Trim();
            var pollType = EffectivePollType(round.Module);
            if (!PollRules.ValidAnswer(pollType, Choices(round.Module).Length, request.Choice, text)
                || text?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true
                || round.Module.Kind == "word-cloud" && text is { } word && (word.Length > 60 || word.Any(char.IsControl)))
                return BadRequest(new { error = "invalid_answer" });

            var own = await db.LiveCourseEntries.Where(e => e.RoundId == round.Id && e.UserSub == access.UserSub)
                .OrderBy(e => e.Slot).ToArrayAsync(ct);
            var many = CanVote(round.Module);
            var previous = many
                ? own.FirstOrDefault(e => string.Equals(e.Text, text, StringComparison.OrdinalIgnoreCase))
                : own.FirstOrDefault();
            if (previous is not null && (many || previous.Choice == request.Choice && previous.Text == text)) return null;
            if (previous is not null && round.Module.Kind == "quiz") return Conflict(new { error = "quiz_already_answered" });
            if (previous is null && (own.Length >= MaxAnswersPerParticipant
                || await db.LiveCourseEntries.CountAsync(e => e.RoundId == round.Id, ct) >= MaxEntriesPerRound))
                return Conflict(new { error = "answer_limit" });

            var entry = previous ?? new LiveCourseEntry
            {
                Id = Guid.NewGuid(), RoundId = round.Id, UserSub = access.UserSub, Slot = own.Length
            };
            entry.Choice = request.Choice;
            entry.Text = pollType == "text" ? text : null;
            entry.Approved = pollType == "text" && round.Module.Kind != "word-cloud" ? null : true;
            entry.UpdatedAtUtc = now;
            if (previous is null) db.LiveCourseEntries.Add(entry);
            return null;
        }, ct);

    [HttpPost("{sessionId:guid}/rounds/{roundId:guid}/entries/{entryId:guid}/moderate")]
    public Task<ActionResult<LiveCourseView>> Moderate(Guid pageId, Guid sessionId, Guid roundId, Guid entryId,
        ModerateLiveCourseEntry request, CancellationToken ct) =>
        Change(pageId, sessionId, true, async (session, _, _) =>
        {
            var round = await ActiveRound(session, roundId, ct);
            if (round is null || EffectivePollType(round.Module) != "text") return NotFound();
            var entry = await db.LiveCourseEntries.FirstOrDefaultAsync(e => e.RoundId == roundId && e.Id == entryId, ct);
            if (entry is null) return NotFound();
            entry.Approved = request.Approved;
            return null;
        }, ct);

    [HttpPost("{sessionId:guid}/rounds/{roundId:guid}/entries/{entryId:guid}/vote")]
    public Task<ActionResult<LiveCourseView>> Vote(Guid pageId, Guid sessionId, Guid roundId, Guid entryId,
        VoteLiveCourseEntry request, CancellationToken ct) =>
        Change(pageId, sessionId, false, async (session, access, now) =>
        {
            var round = await ActiveRound(session, roundId, ct);
            if (round is null || !IsOpen(round, now) || !CanVote(round.Module) || !await db.LiveCourseEntries.AnyAsync(e =>
                e.Id == entryId && e.RoundId == roundId && e.Approved == true, ct)) return NotFound();
            var vote = await db.LiveCourseVotes.FindAsync([entryId, access.UserSub], ct);
            if (request.Voted && vote is null) db.LiveCourseVotes.Add(new() { EntryId = entryId, UserSub = access.UserSub });
            if (!request.Voted && vote is not null) db.LiveCourseVotes.Remove(vote);
            return null;
        }, ct);

    private Task<LiveCourseRound?> ActiveRound(LiveCourseSession session, Guid? id, CancellationToken ct) =>
        id == null || session.ActiveRoundId != id ? Task.FromResult<LiveCourseRound?>(null)
            : db.LiveCourseRounds.Include(r => r.Module).FirstOrDefaultAsync(r => r.Id == id && r.SessionId == session.Id, ct);

    private static bool IsOpen(LiveCourseRound round, DateTime now) => round.ClosedAtUtc is null
        && (round.DeadlineUtc is null || round.DeadlineUtc > now);

    private static bool CanVote(LiveCourseModule module) => module.Kind is "qa" or "brainstorm";

    private static string EffectivePollType(LiveCourseModule module) => module.Kind switch
    {
        "poll" => module.PollType ?? "choice",
        "quiz" => "choice",
        _ => "text"
    };

    private static string[] Choices(LiveCourseModule module) => JsonSerializer.Deserialize<string[]>(module.OptionsJson) ?? [];

    private static LiveCourseModuleView ModuleView(LiveCourseModule module) => new(
        module.Id, module.BlockId, module.Kind, module.Prompt, Choices(module), module.CorrectChoice, module.DurationSeconds, module.PollType);

    private static bool TryModule(Guid pageId, SaveLiveCourseModule request, DateTime now, out LiveCourseModule module)
    {
        module = new();
        var prompt = request.Prompt?.Trim();
        if (!ValidBlockId(request.BlockId) || prompt is not { Length: > 0 and <= 200 } || prompt.Any(char.IsControl)
            || request.Kind is not ("poll" or "brainstorm" or "word-cloud" or "qa" or "quiz")) return false;
        var pollType = request.Kind == "poll" ? request.PollType ?? "choice" : null;
        if (pollType is not (null or "choice" or "mood" or "scale" or "text")) return false;
        var options = PollRules.Options(pollType ?? (request.Kind == "quiz" ? "choice" : "text"), request.Options);
        if ((request.Kind == "quiz" || pollType == "choice") && (!PollRules.ValidChoices(options)
            || options.Any(o => o.Any(char.IsControl)))) return false;
        if (request.Kind == "quiz")
        {
            if (request.CorrectChoice is not int index || index < 0 || index >= options.Length
                || request.DurationSeconds is < 5 or > 300) return false;
        }
        else if (request.CorrectChoice != null || request.DurationSeconds != null) return false;

        module = new()
        {
            Id = Guid.NewGuid(), PageId = pageId, BlockId = request.BlockId, Kind = request.Kind, PollType = pollType,
            Prompt = prompt, OptionsJson = JsonSerializer.Serialize(options), CorrectChoice = request.CorrectChoice,
            DurationSeconds = request.DurationSeconds, CreatedAtUtc = now
        };
        return true;
    }
}
