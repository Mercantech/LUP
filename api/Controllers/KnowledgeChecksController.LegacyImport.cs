using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class KnowledgeChecksController
{
    private static readonly Lazy<LegacyTemplate[]> LegacyTemplates = new(LoadLegacyTemplates);

    [HttpPost("legacy-import")]
    public async Task<IActionResult> ImportLegacy(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();

        var templates = LegacyTemplates.Value.ToDictionary(template => LegacyDraftId(sub, template.Key));
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

    private static Guid LegacyDraftId(Guid sub, string key)
    {
        // A stable owner/source key prevents duplicates even when the imported title is edited.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"lup:videnstjek:legacy:v1:{sub:N}:{key}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static LegacyTemplate[] LoadLegacyTemplates()
    {
        using var source = typeof(KnowledgeChecksController).Assembly.GetManifestResourceStream("Mercantec.Tech.Api.LegacyKnowledgeChecks.json")
            ?? throw new InvalidDataException("Missing embedded legacy knowledge checks.");
        var bundle = JsonSerializer.Deserialize<LegacyBundle>(source, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (bundle?.Drafts is not { Length: > 0 } drafts) throw new InvalidDataException("No legacy knowledge checks.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        return drafts.Select(template =>
        {
            if (string.IsNullOrWhiteSpace(template.Key) || !keys.Add(template.Key)
                || template.Draft is null || !NormalizeDraft(template.Draft, out var draft, out _)
                || draft.Questions.Length == 0 || draft.Questions.Any(question => !IsReady(question)))
                throw new InvalidDataException($"Invalid embedded legacy knowledge check '{template.Key}'.");

            return template with { Draft = draft };
        }).ToArray();
    }

    private sealed record LegacyBundle(LegacyTemplate[] Drafts);
    private sealed record LegacyTemplate(string Key, KnowledgeCheckDraft Draft);
}
