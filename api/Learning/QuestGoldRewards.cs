using Mercantec.Tech.Api.Avatars;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

internal static class QuestGoldRewards
{
    internal static async Task<bool> TryAward(AppDbContext db, LearningQuest quest,
        SubmissionRevision revision, SubmissionFeedback feedback, CancellationToken ct)
    {
        var accounts = revision.Participants.Select(participant => participant.UserSub)
            .OfType<Guid>().ToHashSet();
        var unlinkedMemberships = revision.Participants.Where(participant => participant.UserSub is null)
            .Select(participant => participant.MembershipId).ToArray();
        if (unlinkedMemberships.Length > 0)
        {
            // A later account link can resolve the original membership without
            // adding new group members to the submitted snapshot.
            var linked = await db.Memberships.AsNoTracking()
                .Where(member => member.HoldId == quest.HoldId && unlinkedMemberships.Contains(member.Id)
                    && member.UserSub != null)
                .Select(member => member.UserSub!.Value).ToArrayAsync(ct);
            accounts.UnionWith(linked);
        }

        foreach (var subject in accounts.Order())
        {
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO quest_gold_awards ("QuestId", "UserSub", "FeedbackId", "Gold", "AwardedAtUtc")
                VALUES ({quest.Id}, {subject}, {feedback.Id}, {QuestRewards.GoldOnApproval}, {feedback.CreatedAtUtc})
                ON CONFLICT ("QuestId", "UserSub") DO NOTHING
                """, ct);
            if (inserted > 0 && !await AvatarWallet.TryAdjustGold(db, subject, QuestRewards.GoldOnApproval, ct))
                return false;
        }

        return true;
    }
}
