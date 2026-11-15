using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notion;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.KnowledgeChecks;

public static class KnowledgeCheckNotebookAccess
{
    public static async Task<bool> CanReadAsync(AppDbContext db, Guid pageId, string blockId,
        Guid publicationId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!ValidBlockId(blockId)) return false;
        var pages = new NotionAccess(db, new CourseAccess(db));
        var page = await pages.ReadablePages(user, publishedOnly: true).AsNoTracking().Where(p => p.Id == pageId)
            .Select(p => new { p.HoldId, p.PublishedKnowledgeChecksJson }).SingleOrDefaultAsync(ct);
        if (page is null || !Bindings(page.PublishedKnowledgeChecksJson).TryGetValue(blockId, out var bound)
            || bound != publicationId) return false;
        return await Options(db, page.HoldId).AnyAsync(p => p.Id == publicationId, ct);
    }

    internal static IQueryable<KnowledgeCheckPublication> Options(AppDbContext db, Guid? holdId) =>
        db.KnowledgeCheckPublications.AsNoTracking().Where(p => p.IsActive && (p.HoldId == null || p.HoldId == holdId));

    internal static async Task<bool> CanPublishAsync(AppDbContext db, Guid? holdId, string manifest, CancellationToken ct)
    {
        var ids = Bindings(manifest).Values.Distinct().ToArray();
        if (ids.Length == 0) return true;
        return await Options(db, holdId).CountAsync(p => ids.Contains(p.Id), ct) == ids.Length;
    }

    internal static bool ValidBlockId(string blockId) => blockId.Length is > 0 and <= 128
        && blockId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static Dictionary<string, Guid> Bindings(string manifest) =>
        JsonSerializer.Deserialize<Dictionary<string, Guid>>(manifest) ?? [];
}
