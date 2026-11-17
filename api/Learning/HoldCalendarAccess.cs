using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;

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
}
