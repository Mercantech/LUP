using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.References;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Public;
using Microsoft.EntityFrameworkCore;
using Category = Mercantec.Tech.Contracts.GlobalSearchCategories;

namespace Mercantec.Tech.Api.Search;

internal sealed class GlobalSearchService(AppDbContext db, TimeProvider clock)
{
    private readonly CourseAccess courses = new(db);
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private static readonly IReadOnlyDictionary<string, (string Category, string Kind)> ReferenceKinds =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["quest"] = (Category.Assignments, "Opgave"),
            ["submission"] = (Category.Assignments, "Aflevering"),
            ["teaching-course"] = (Category.Courses, "Forløb"),
            ["material"] = (Category.Materials, "Materiale"),
            ["notion-page"] = (Category.Notes, "Undervisningsside"),
            ["notebook-page"] = (Category.Notes, "Personlig note"),
            ["collection"] = (Category.Notes, "Samling"),
            ["notebook"] = (Category.Notes, "Notesbog"),
            ["hold"] = (Category.Holds, "Hold"),
            ["group"] = (Category.Holds, "Gruppe"),
            ["event"] = (Category.Events, "Kalenderbegivenhed"),
            ["poll"] = (Category.Polls, "Afstemning"),
            ["file"] = (Category.Files, "Fil"),
            ["notion-asset"] = (Category.Files, "Vedhæftning"),
            ["notebook-asset"] = (Category.Files, "Vedhæftning"),
            ["drawing"] = (Category.Files, "Tegning")
        };

    public async Task<GlobalSearchResult> SearchAsync(ClaimsPrincipal user, Guid sub, string query, string category,
        int offset, int limit, bool publishedOnly, CancellationToken ct)
    {
        bool Includes(string candidate) => category == Category.All || category == candidate;

        var publicHits = PublicGlobalSearch.Search(query, category);
        var privateHits = new List<GlobalSearchHit>();
        if (Includes(Category.Projects)) privateHits.AddRange(await OwnProjectsAsync(user, sub, ct));
        var kinds = ReferenceKinds.Where(pair => Includes(pair.Value.Category)).Select(pair => pair.Key).ToHashSet();
        if (kinds.Count > 0)
        {
            var references = await new ReferenceService(db).GlobalCatalogAsync(user, query, publishedOnly, kinds, ct);
            foreach (var reference in references)
            {
                var (facet, kind) = ReferenceKinds[reference.Reference.Kind];
                privateHits.Add(new(facet, kind, reference.Title, reference.Href, reference.ContextLabel));
            }
        }
        if (Includes(Category.People)) privateHits.AddRange(await PeopleAsync(user, sub, ct));
        if (Includes(Category.KnowledgeChecks)) privateHits.AddRange(await KnowledgeChecksAsync(user, sub, publishedOnly, ct));
        if (Includes(Category.Electives)) privateHits.AddRange(await ElectivesAsync(user, publishedOnly, ct));
        if (Includes(Category.Pages)) privateHits.AddRange(PublicGlobalSearch.MemberPages());

        var matches = publicHits.Concat(privateHits.Where(hit => PublicGlobalSearch.Matches(hit, query)))
            .DistinctBy(hit => hit.Href, StringComparer.Ordinal);
        var ordered = PublicGlobalSearch.Rank(matches, query).ToArray();
        var items = ordered.Skip(offset).Take(limit).Select(hit => hit.Description is { Length: > 200 } description
            ? hit with { Description = description[..(char.IsHighSurrogate(description[199]) ? 199 : 200)].TrimEnd() + "…" } : hit);
        return new(items.ToArray(), ordered.Length);
    }

    private async Task<List<GlobalSearchHit>> PeopleAsync(ClaimsPrincipal user, Guid sub, CancellationToken ct)
    {
        var holds = courses.AccessibleHolds(user).AsNoTracking();
        var holdIds = holds.Select(hold => hold.Id);
        var members = await db.Memberships.AsNoTracking().Where(member => holdIds.Contains(member.HoldId))
            .Select(member => new { member.Id, member.HoldId, member.UserSub, member.DisplayName, HoldName = member.Hold.Name })
            .ToArrayAsync(ct);
        var teachers = await holds.Select(hold => new { Sub = hold.Forloeb.CreatedBySub, HoldName = hold.Name }).ToArrayAsync(ct);
        var visibleSubjects = members.Where(member => member.UserSub != null).Select(member => member.UserSub!.Value)
            .Concat(teachers.Select(teacher => teacher.Sub)).Append(sub).Distinct().ToArray();
        var profiles = await db.NotebookProfiles.AsNoTracking().Where(profile => visibleSubjects.Contains(profile.Sub))
            .ToDictionaryAsync(profile => profile.Sub, ct);
        var names = await db.ProfileNames.AsNoTracking().Where(profile => visibleSubjects.Contains(profile.UserSub))
            .ToDictionaryAsync(profile => profile.UserSub, profile => profile.DisplayName, ct);

        string? Name(Guid? subject, string? fallback) => subject is Guid id
            ? names.GetValueOrDefault(id) ?? profiles.GetValueOrDefault(id)?.DisplayName ?? fallback : fallback;

        var results = new List<GlobalSearchHit>();
        if (Name(sub, UserClaims.EditorName(user)) is { Length: > 0 } ownName)
            results.Add(new(Category.People, "Person", ownName, "/profile", "Min profil"));

        var includedSubjects = new HashSet<Guid> { sub };
        foreach (var person in members.Where(member => member.UserSub != sub).GroupBy(member => member.UserSub ?? member.Id))
        {
            var member = person.OrderBy(row => row.DisplayName == null).ThenBy(row => row.HoldName).ThenBy(row => row.Id).First();
            var name = Name(member.UserSub, member.DisplayName);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var holdNames = string.Join(" · ", person.Select(row => row.HoldName).Distinct().Order(StringComparer.CurrentCultureIgnoreCase));
            var href = $"/notion/holds/{member.HoldId:D}/students/{member.Id:D}";
            if (member.UserSub is Guid account)
            {
                includedSubjects.Add(account);
                if (profiles.ContainsKey(account)) href = $"/people/{account:D}";
            }

            var alias = member.DisplayName != name ? member.DisplayName : null;
            results.Add(new(Category.People, "Person", name, href, holdNames, alias));
        }

        foreach (var teacher in teachers.Where(row => !includedSubjects.Contains(row.Sub)).GroupBy(row => row.Sub))
        {
            if (!profiles.ContainsKey(teacher.Key) || Name(teacher.Key, null) is not { Length: > 0 } name) continue;
            var context = string.Join(" · ", teacher.Select(row => row.HoldName).Distinct().Order(StringComparer.CurrentCultureIgnoreCase));
            results.Add(new(Category.People, "Underviser", name, $"/people/{teacher.Key:D}", context));
        }
        return results;
    }

    private async Task<IEnumerable<GlobalSearchHit>> OwnProjectsAsync(ClaimsPrincipal user, Guid sub, CancellationToken ct)
    {
        var allowed = courses.AccessibleHolds(user).Select(hold => hold.Id);
        var games = await db.UploadedGames.AsNoTracking().Where(game => game.OwnerSub == sub && game.FileAsset.OwnerSub == sub
                && (game.FileAsset.HoldId == null || allowed.Contains(game.FileAsset.HoldId.Value)))
            .Select(game => new { game.Title, game.FileAssetId, game.FileAsset.OriginalName }).ToArrayAsync(ct);
        return games.Select(game => new GlobalSearchHit(Category.Projects, "Spilprojekt", game.Title,
            $"/references/file/file/{game.FileAssetId:D}", "Mine projekter", game.OriginalName));
    }

    private async Task<List<GlobalSearchHit>> KnowledgeChecksAsync(ClaimsPrincipal user, Guid sub, bool publishedOnly, CancellationToken ct)
    {
        var canSeeDrafts = !publishedOnly && UserClaims.IsTeacher(user);
        var allowed = courses.AccessibleHolds(user).Select(hold => hold.Id);
        var managed = courses.AccessibleHolds(user, true).Select(hold => hold.Id);
        var publications = await db.KnowledgeCheckPublications.AsNoTracking().Where(publication => publication.HoldId == null
            ? publication.IsActive || canSeeDrafts && publication.Version.Quiz.OwnerSub == sub
            : allowed.Contains(publication.HoldId.Value) && (publication.IsActive || canSeeDrafts && managed.Contains(publication.HoldId.Value)))
            .Select(publication => new { publication.Id, publication.Version.Title, publication.Version.Topic, publication.Version.Description,
                HoldName = publication.Hold == null ? null : publication.Hold.Name }).ToArrayAsync(ct);
        var hits = publications.Select(publication => new GlobalSearchHit(Category.KnowledgeChecks, "Videnstjek", publication.Title,
            $"/videnstjek/{publication.Id:D}", publication.HoldName ?? publication.Topic, publication.Description)).ToList();

        if (canSeeDrafts)
        {
            var drafts = await db.KnowledgeCheckQuizzes.AsNoTracking().Where(quiz => quiz.OwnerSub == sub)
                .Select(quiz => new { quiz.Id, quiz.Title, quiz.Topic, quiz.Description }).ToArrayAsync(ct);
            hits.AddRange(drafts.Select(quiz => new GlobalSearchHit(Category.KnowledgeChecks, "Videnstjek · kladde", quiz.Title,
                $"/videnstjek/rediger/{quiz.Id:D}", quiz.Topic, quiz.Description)));
        }
        return hits;
    }

    private async Task<IEnumerable<GlobalSearchHit>> ElectivesAsync(ClaimsPrincipal user, bool publishedOnly, CancellationToken ct)
    {
        var canManage = !publishedOnly && UserClaims.IsTeacher(user);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Copenhagen).DateTime);
        var electives = await db.Electives.AsNoTracking().Where(elective => canManage || elective.Status != "draft")
            .Select(elective => new { elective.Id, elective.Status, elective.Title, elective.Description, elective.StartsOn, elective.EndsOn })
            .ToArrayAsync(ct);
        return electives.Select(elective =>
        {
            var (view, context) = elective.Status switch
            {
                "template" => ("katalog", "Fagkatalog"),
                "draft" => ("administrer", "Kladde"),
                "archived" => ("historik", "Tidligere hold"),
                _ when (elective.EndsOn ?? elective.StartsOn) < today => ("historik", "Tidligere hold"),
                _ => ("aktuelle", "Aktuelle hold")
            };
            return new GlobalSearchHit(Category.Electives, "Valgfag", elective.Title, $"/valgfag?vis={view}&id={elective.Id:D}", context, elective.Description);
        });
    }
}
