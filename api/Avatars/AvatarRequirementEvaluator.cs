using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Gifts;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.Avatars;

public static class AvatarRequirementEvaluator
{
    public static async Task<IReadOnlyList<AvatarUnlockProgressDto>> EvaluateAsync(
        AppDbContext db, Guid subject, IReadOnlyList<AvatarUnlockDefinition> avatars, CancellationToken ct)
    {
        var result = new List<AvatarUnlockProgressDto>();
        var counts = new Dictionary<(string Kind, string Target), int>();
        foreach (var avatar in avatars)
        foreach (var requirement in avatar.Requirements ?? [])
        {
            // A new or invalid requirement must not silently turn into a free
            // unlock when a deployment has not implemented its evaluator yet.
            var supported = requirement.RequiredCount > 0 && (requirement.Kind switch
            {
                AvatarRequirementKinds.SentGiftsDistinctRecipients => GiftThemes.IsKnown(requirement.Target),
                AvatarRequirementKinds.UploadedGames => requirement.Target == "site",
                AvatarRequirementKinds.SubmittedKnowledgeChecks => requirement.Target == "backend",
                _ => false
            });
            var current = 0;
            if (supported)
            {
                var key = (requirement.Kind, requirement.Target);
                if (!counts.TryGetValue(key, out current))
                {
                    current = requirement.Kind switch
                    {
                        AvatarRequirementKinds.UploadedGames => await db.UploadedGames.AsNoTracking()
                            .CountAsync(game => game.OwnerSub == subject && game.FileAsset.OwnerSub == subject, ct),
                        AvatarRequirementKinds.SubmittedKnowledgeChecks => await CountKnowledgeChecksAsync(db, subject, requirement.Target, ct),
                        AvatarRequirementKinds.SentGiftsDistinctRecipients => await db.ChristmasGifts.AsNoTracking()
                            .Where(gift => gift.SenderSub == subject && gift.Theme == requirement.Target
                                && gift.RecipientSub != Guid.Empty && gift.RecipientSub != subject)
                            .Select(gift => gift.RecipientSub).Distinct().CountAsync(ct),
                        _ => 0
                    };
                    counts[key] = current;
                }
            }
            result.Add(new(avatar.Id, requirement.Id, current, requirement.RequiredCount,
                supported && current >= requirement.RequiredCount));
        }
        return result;
    }

    private static async Task<int> CountKnowledgeChecksAsync(AppDbContext db, Guid subject, string topic, CancellationToken ct)
    {
        var completed = await db.KnowledgeCheckAttempts.AsNoTracking()
            .Where(attempt => attempt.UserSub == subject && attempt.SubmittedAtUtc != null
                && (attempt.Status == Submitted || attempt.Status == Graded)
                && (attempt.Publication.HoldId == null || attempt.Publication.Version.Topic.ToLower() == topic))
            .Select(attempt => new
            {
                attempt.Publication.Version.QuizId,
                attempt.Publication.Version.Topic,
                attempt.Publication.Version.Quiz.OwnerSub,
                IsPractice = attempt.Publication.HoldId == null
            })
            .Distinct().ToArrayAsync(ct);

        var legacyByOwner = completed.Where(check => check.IsPractice)
            .Select(check => check.OwnerSub).Distinct()
            .ToDictionary(owner => owner, LegacyKnowledgeCheckCatalog.ForOwner);

        return completed
            .Where(check => string.Equals(check.Topic, topic, StringComparison.OrdinalIgnoreCase)
                || (check.IsPractice && legacyByOwner[check.OwnerSub].TryGetValue(check.QuizId, out var legacy)
                    && legacy.DiscoveryForTopic(check.Topic)?.Categories.Contains(topic, StringComparer.OrdinalIgnoreCase) == true))
            .Select(check => check.QuizId).Distinct().Count();
    }
}
