using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class LiveCourseController
{
    [HttpGet("history")]
    public async Task<ActionResult<LiveCourseHistory[]>> History(Guid pageId, CancellationToken ct)
    {
        if (await ReadPage(pageId, ct) is not { MayManage: true } access) return NotFound();
        var now = time.GetUtcNow().UtcDateTime;
        var sessions = await db.LiveCourseSessions.AsNoTracking().Where(s => s.PageId == pageId && s.HoldId == access.HoldId
                && (s.EndedAtUtc != null || s.ExpiresAtUtc <= now || s.PublicationVersion != access.PublicationVersion
                    || s.PublishedRevision != access.PublishedRevision))
            .OrderByDescending(s => s.StartedAtUtc).Take(10).ToArrayAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToArray();
        var rounds = await db.LiveCourseRounds.AsNoTracking().Where(r => sessionIds.Contains(r.SessionId))
            .Select(r => new { Round = r, r.Module }).OrderBy(r => r.Round.OpenedAtUtc).ToListAsync(ct);
        var roundIds = rounds.Select(r => r.Round.Id).ToArray();
        var totals = await db.LiveCourseEntries.AsNoTracking().Where(e => roundIds.Contains(e.RoundId))
            .GroupBy(e => new { e.RoundId, e.Choice })
            .Select(g => new { g.Key.RoundId, g.Key.Choice, Count = g.Count() }).ToArrayAsync(ct);
        var participants = await (
            from entry in db.LiveCourseEntries.AsNoTracking()
            join round in db.LiveCourseRounds.AsNoTracking() on entry.RoundId equals round.Id
            where sessionIds.Contains(round.SessionId)
            select new { round.SessionId, entry.UserSub })
            .Distinct().GroupBy(e => e.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() }).ToDictionaryAsync(e => e.SessionId, e => e.Count, ct);
        var totalsByRound = totals.ToLookup(t => t.RoundId);
        var summaries = rounds.ToLookup(r => r.Round.SessionId, r =>
        {
            var options = Choices(r.Module);
            var counts = EffectivePollType(r.Module) == "text" ? null : new int[options.Length];
            if (counts is not null)
                foreach (var t in totalsByRound[r.Round.Id])
                    if (t.Choice is int index && index >= 0 && index < counts.Length) counts[index] = t.Count;
            return new LiveCourseRoundSummary(r.Round.Id, r.Module.BlockId, r.Module.Kind, r.Module.Prompt,
                options, totalsByRound[r.Round.Id].Sum(t => t.Count), counts, r.Module.PollType);
        });
        return Ok(sessions.Select(s => new LiveCourseHistory(s.Id, Stamp(s.StartedAtUtc),
            Stamp(s.EndedAtUtc ?? (s.ExpiresAtUtc <= now ? s.ExpiresAtUtc : now)), s.PublishedRevision,
            participants.GetValueOrDefault(s.Id), summaries[s.Id].ToArray())).ToArray());
    }

    private async Task<LiveCourseView> BuildView(LiveCourseSession session, PageAccess access, DateTime now, CancellationToken ct)
    {
        if (session.EndedAtUtc is not null) return new(null);
        var reactionCutoff = now - ReactionLifetime;
        var reactions = await db.LiveCourseReactions.AsNoTracking().Where(r => r.SessionId == session.Id && r.UpdatedAtUtc >= reactionCutoff)
            .GroupBy(r => r.Emoji).Select(g => new LiveCourseReactionCount(g.Key, g.Count())).ToArrayAsync(ct);
        var round = session.ActiveRoundId is Guid id
            ? await db.LiveCourseRounds.AsNoTracking().Include(r => r.Module)
                .FirstOrDefaultAsync(r => r.Id == id && r.SessionId == session.Id, ct)
            : null;
        return new(new(session.Id, session.PublishedRevision, session.SlideIndex, session.Revision, access.MayManage,
            reactions, round is null ? null : await BuildRound(round, access, now, ct), session.RankingVisible,
            session.RankingVisible ? await BuildRanking(session.Id, ct) : []));
    }

    private async Task<LiveCourseRoundView> BuildRound(LiveCourseRound round, PageAccess access, DateTime now, CancellationToken ct)
    {
        var module = round.Module;
        var options = Choices(module);
        var textMode = EffectivePollType(module) == "text";
        var visible = round.ShowResults || access.MayManage;
        var answerQuery = db.LiveCourseEntries.AsNoTracking().Where(e => e.RoundId == round.Id);
        var own = await answerQuery.Where(e => e.UserSub == access.UserSub).OrderByDescending(e => e.Slot).FirstOrDefaultAsync(ct);
        var publicText = !access.MayManage && textMode ? answerQuery.Where(e => e.Approved == true) : answerQuery;

        int[]? counts = null;
        int? total = null;
        if (visible)
        {
            if (textMode) total = await publicText.CountAsync(ct);
            else
            {
                counts = PollRules.Counts(options.Length, await answerQuery.Select(e => e.Choice).ToArrayAsync(ct));
                total = counts.Sum();
            }
        }
        var entries = textMode && (visible || CanVote(module))
            ? await BuildEntries(publicText, module, access, ct)
            : [];
        return new(round.Id, module.Id, module.BlockId, module.Kind, module.Prompt, options,
            IsOpen(round, now), round.ShowResults, round.DeadlineUtc is DateTime deadline ? Stamp(deadline) : null,
            own is not null, own?.Choice, own?.Approved == true ? own.Text : null, counts,
            round.ShowResults && module.Kind == "quiz" ? module.CorrectChoice : null, entries, total, module.PollType);
    }

    private async Task<LiveCourseEntryView[]> BuildEntries(IQueryable<LiveCourseEntry> query, LiveCourseModule module,
        PageAccess access, CancellationToken ct)
    {
        if (module.Kind == "word-cloud")
        {
            var words = await query.Where(e => e.Text != null).OrderBy(e => e.Id).ToArrayAsync(ct);
            var counts = words.Where(e => e.Approved == true)
                .GroupBy(e => e.Text!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            if (!access.MayManage) words = words.GroupBy(e => e.Text, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
            return words.Select(w => EntryView(w, access, w.Approved == true ? access.MayManage ? 1 : counts[w.Text!] : 0, false))
                .OrderByDescending(w => w.Votes).ThenBy(w => w.Text, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var entries = await query.Where(e => e.Text != null).OrderBy(e => e.Id).Select(e => new
        {
            Entry = e,
            Count = db.LiveCourseVotes.Count(v => v.EntryId == e.Id),
            OwnVote = db.LiveCourseVotes.Any(v => v.EntryId == e.Id && v.UserSub == access.UserSub)
        }).ToArrayAsync(ct);
        return entries.Select(e => EntryView(e.Entry, access, e.Count, e.OwnVote))
            .OrderByDescending(e => e.Votes).ToArray();
    }

    private static LiveCourseEntryView EntryView(LiveCourseEntry answer, PageAccess access, int votes, bool ownVote) =>
        new(answer.Id, answer.Text!, answer.Approved == true, votes, ownVote, answer.UserSub == access.UserSub,
            answer.Approved is not null);

    private async Task<LiveCourseRank[]> BuildRanking(Guid sessionId, CancellationToken ct)
    {
        var scores = await (from answer in db.LiveCourseEntries.AsNoTracking()
            join round in db.LiveCourseRounds.AsNoTracking() on answer.RoundId equals round.Id
            where round.SessionId == sessionId && round.Module.Kind == "quiz" && round.ShowResults
            group new { answer.Choice, round.Module.CorrectChoice } by answer.UserSub into participant
            select new { UserSub = participant.Key, Score = participant.Count(a => a.Choice == a.CorrectChoice) }).ToArrayAsync(ct);
        return scores.OrderBy(s => s.UserSub).Select((s, index) => new LiveCourseRank($"Deltager {index + 1}", s.Score))
            .OrderByDescending(s => s.Score).Take(30).ToArray();
    }

    private static DateTimeOffset Stamp(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Utc));
}
