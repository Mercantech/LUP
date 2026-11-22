using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

public sealed class HoldCalendarAccess(AppDbContext db)
{
    public IQueryable<Guid> AssignedQuestIds(Guid student) => new QuestAudienceAccess(db)
        .AssignedTo(db.LearningQuests, student).Select(q => q.Id);

    public IQueryable<HoldCalendarEvent> Visible(IQueryable<HoldCalendarEvent> events, Guid viewer, Guid[] manageableHoldIds)
    {
        var assigned = AssignedQuestIds(viewer);
        return events.Where(e => e.QuestId == null || manageableHoldIds.Contains(e.HoldId) || assigned.Contains(e.QuestId.Value));
    }

    internal async Task<HoldCalendarOverview> ReadOverviewAsync(IQueryable<Hold> visible, IQueryable<Hold> manageable,
        Guid viewer, DateTimeOffset start, DateTimeOffset end, CancellationToken ct,
        bool preview = false, bool includeQuestChoices = true)
    {
        var sources = await visible.AsNoTracking().OrderBy(h => h.Name).ThenBy(h => h.Id)
            .Select(h => new HoldCalendarSource(h.Id, h.ForloebId, h.Name, !preview && manageable.Any(m => m.Id == h.Id)))
            .ToArrayAsync(ct);
        if (sources.Length == 0) return new([], [], [], []);

        var holdIds = sources.Select(h => h.HoldId).ToArray();
        var manageableIds = sources.Where(h => h.CanManage).Select(h => h.HoldId).ToArray();
        var events = Visible(db.HoldCalendarEvents.AsNoTracking().Where(e => holdIds.Contains(e.HoldId)), viewer, manageableIds);
        var assigned = AssignedQuestIds(viewer);
        var quests = db.LearningQuests.AsNoTracking().Where(q => q.Published && holdIds.Contains(q.HoldId));
        var deadlines = quests.Where(q => q.DeadlineUtc != null && (manageableIds.Contains(q.HoldId) || assigned.Contains(q.Id)));
        // Production uses PostgreSQL. SQLite test databases cannot compare DateTimeOffset values in SQL.
        var postgres = db.Database.IsNpgsql();
        if (postgres)
        {
            events = events.Where(e => e.EndUtc > start && e.StartUtc < end);
            deadlines = deadlines.Where(q => q.DeadlineUtc >= start && q.DeadlineUtc < end);
        }
        var eventRows = await events.ToArrayAsync(ct);
        var deadlineRows = await deadlines.Select(q => new CalendarQuestDeadline(q.Id, q.HoldId, q.Title, q.DeadlineUtc!.Value))
            .ToArrayAsync(ct);
        if (!postgres)
        {
            eventRows = eventRows.Where(e => e.EndUtc > start && e.StartUtc < end).ToArray();
            deadlineRows = deadlineRows.Where(q => q.DeadlineUtc >= start && q.DeadlineUtc < end).ToArray();
        }
        CalendarQuestChoice[] choices = !includeQuestChoices || manageableIds.Length == 0 ? []
            : await quests.Where(q => manageableIds.Contains(q.HoldId)).OrderBy(q => q.Title).ThenBy(q => q.Id)
                .Select(q => new CalendarQuestChoice(q.Id, q.HoldId, q.Title)).ToArrayAsync(ct);
        return new(sources, eventRows.OrderBy(e => e.StartUtc).ThenBy(e => e.Id).Select(View).ToArray(),
            deadlineRows.OrderBy(q => q.DeadlineUtc).ThenBy(q => q.QuestId).ToArray(), choices);
    }

    internal static HoldCalendarEventView View(HoldCalendarEvent e) =>
        new(e.Id, e.HoldId, e.Title, e.Description, e.StartUtc, e.EndUtc, e.Location, e.Kind, e.Version, e.AttendanceMode, e.QuestId);
}
