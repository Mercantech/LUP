using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class KnowledgeChecksController
{
    [HttpPost("legacy-import")]
    public async Task<IActionResult> ImportLegacy(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();

        var templates = LegacyKnowledgeCheckCatalog.ForOwner(sub);
        var ids = templates.Keys.ToArray();
        var existingQuery = db.KnowledgeCheckQuizzes.Where(quiz => quiz.OwnerSub == sub && ids.Contains(quiz.Id))
            .Select(quiz => quiz.Id);
        var existing = await existingQuery.ToHashSetAsync(ct);
        while (existing.Count < templates.Count)
        {
            var now = DateTime.UtcNow;
            var pending = templates.Where(item => !existing.Contains(item.Key))
                .OrderBy(item => item.Key).Select(item => NewDraftQuiz(item.Key, sub, item.Value.Draft, now)).ToArray();
            db.KnowledgeCheckQuizzes.AddRange(pending);
            try
            {
                await db.SaveChangesAsync(ct);
                return Ok(new KnowledgeCheckLegacyImport(pending.Length, existing.Count));
            }
            catch (DbUpdateException ex) when (NotebookPeriods.IsUniqueViolation(ex))
            {
                // This SaveChanges is atomic. Reload after a concurrent import without overwriting its drafts.
                foreach (var draft in pending) db.Entry(draft).State = EntityState.Detached;
                var current = await existingQuery.ToHashSetAsync(ct);
                if (current.IsSubsetOf(existing)) throw;
                existing = current;
            }
        }

        return Ok(new KnowledgeCheckLegacyImport(0, existing.Count));
    }
}
