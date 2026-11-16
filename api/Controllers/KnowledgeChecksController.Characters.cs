using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class KnowledgeChecksController
{
    [AllowAnonymous]
    [HttpGet("characters/{slug}")]
    public async Task<KnowledgeCheckCharacterSummary[]> CharacterExercises(string slug, CancellationToken ct)
    {
        var ids = CharacterKnowledgeCheckCatalog.QuizIds(slug);
        if (ids.Length == 0) return [];

        // Public character profiles only need the fields displayed on their exercise cards.
        var rows = await ActiveCharacterPublications(ids)
            .Select(row => new
            {
                row.Id, row.Version.QuizId, row.Version.Title, row.Version.Topic,
                row.Version.EstimatedMinutes, row.Version.QuestionCount
            }).ToArrayAsync(ct);

        return rows.DistinctBy(row => row.QuizId)
            .Select(row => new KnowledgeCheckCharacterSummary(row.Id, row.Title, row.Topic,
                row.EstimatedMinutes, row.QuestionCount, CharacterKnowledgeCheckCatalog.All[row.QuizId].Difficulty))
            .ToArray();
    }

    private IOrderedQueryable<KnowledgeCheckPublication> ActiveCharacterPublications(Guid[] quizIds) =>
        db.KnowledgeCheckPublications.AsNoTracking()
            .Where(row => row.IsActive && row.HoldId == null && quizIds.Contains(row.Version.QuizId))
            .OrderByDescending(row => row.CreatedAtUtc).ThenBy(row => row.Id);
}
