using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Characters;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Characters;

public sealed class CharacterArtworkLibrary(AppDbContext db)
{
    private const int PageSize = 12;
    private const int MaxImageBytes = 768 * 1024;
    private const string Cover = "cover";
    private const string Image = "image";
    private const string Collection = "collection";
    private const string NotionPage = "notion-page";
    private const string Notebook = "notebook";
    private const string NotebookPage = "notebook-page";
    private readonly CourseAccess courses = new(db);
    private readonly NotebookAccess notebooks = new(db);

    public void RegisterCover(string slug, string sourceKind, Guid sourceId, Guid coverId, byte[] normalizedWebp) =>
        Register(slug, Cover, sourceKind, sourceId, coverId, normalizedWebp);

    public void RegisterPageImage(string slug, string sourceKind, Guid sourcePageId, Guid assetId, byte[] normalizedWebp)
    {
        if (sourceKind is NotionPage or NotebookPage)
            Register(slug, Image, sourceKind, sourcePageId, assetId, normalizedWebp);
    }

    private void Register(string slug, string kind, string sourceKind, Guid sourceId, Guid assetId, byte[] webp)
    {
        if (CharacterCatalog.Find(slug) is not { Slug: var canonicalSlug }
            || sourceKind is not (Collection or NotionPage or Notebook or NotebookPage)
            || sourceId == Guid.Empty || assetId == Guid.Empty || webp.Length is <= 12 or > MaxImageBytes
            || !webp.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !webp.AsSpan(8, 4).SequenceEqual("WEBP"u8)
            || db.CharacterGeneratedArtwork.Local.Any(item => item.CharacterSlug == canonicalSlug
                && item.SourceKind == sourceKind && item.OriginalAssetId == assetId)
            || NotebookCoverImage.NormalizeEmoji(webp) is not { } preview)
            return;

        db.CharacterGeneratedArtwork.Add(new()
        {
            Id = Guid.NewGuid(), CharacterSlug = canonicalSlug, Kind = kind, SourceKind = sourceKind,
            OriginalAssetId = assetId, CreatedAtUtc = DateTime.UtcNow, Image = webp, Thumbnail = preview,
            CollectionId = sourceKind == Collection ? sourceId : null,
            NotionPageId = sourceKind == NotionPage ? sourceId : null,
            NotebookId = sourceKind == Notebook ? sourceId : null,
            NotebookPageId = sourceKind == NotebookPage ? sourceId : null
        });
    }

    public async Task<CharacterGeneratedArtPage> ListAsync(ClaimsPrincipal user, string? slug, int offset, CancellationToken ct)
    {
        var query = Visible(user);
        if (slug is not null) query = query.Where(item => item.CharacterSlug == slug);
        var rows = await query.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id)
            .Skip(offset).Take(PageSize + 1)
            .Select(item => new ArtRow(item.Id, item.CharacterSlug, item.Kind, item.SourceKind,
                item.CollectionId ?? item.NotionPageId ?? item.NotebookId ?? item.NotebookPageId!.Value, item.CreatedAtUtc))
            .ToArrayAsync(ct);

        var page = rows.Take(PageSize).ToArray();
        var sources = await LoadSources(page, user, ct);
        return new(page.Where(item => sources.ContainsKey((item.SourceKind, item.SourceId))).Select(item =>
        {
            var source = sources[(item.SourceKind, item.SourceId)];
            return new CharacterGeneratedArtItem(item.Id, item.CharacterSlug, item.Kind, item.SourceKind,
                item.SourceId, source.NotebookTitle, source.PageTitle, source.Href,
                new DateTimeOffset(DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc)),
                $"/api/character-artwork/{item.Id:D}/content");
        }).ToArray(), rows.Length > PageSize ? offset + PageSize : null);
    }

    public Task<byte[]?> ReadAsync(Guid id, ClaimsPrincipal user, bool thumbnail, CancellationToken ct) =>
        Visible(user).Where(item => item.Id == id)
            .Select(item => thumbnail ? item.Thumbnail : item.Image).SingleOrDefaultAsync(ct);

    private IQueryable<CharacterGeneratedArtwork> Visible(ClaimsPrincipal user)
    {
        var all = db.CharacterGeneratedArtwork.AsNoTracking();
        if (UserClaims.GetSub(user) is not Guid subject || subject == Guid.Empty)
            return all.Where(_ => false);

        var notion = new NotionAccess(db, courses);
        var teaching = notion.ReadablePages(user, publishedOnly: true);
        var teachingCollections = notion.ReadableCollections(user, publishedOnly: true);
        var editTeaching = notion.ManageablePages(user);
        var editCollections = notion.ManageableCollections(user);
        var readableBooks = notebooks.ReadableWholeNotebooks(user);
        var readableNotes = notebooks.ReadableDocuments(user).Where(note => note.NotebookId.HasValue);
        var editNotes = notebooks.EditableDocuments(user);
        var positionedCovers = db.NotebookCoverAssets.Where(cover => cover.ArtworkSourceId.HasValue);

        // The JSON strings are canonical Guid[] manifests serialized by the existing editor. The
        // full string token, including its quotes, is required so prefix matches cannot grant access.
        // Managers and owners can see archived versions. Readers only see the exact visible version.
        return all.Where(item =>
            item.CollectionId.HasValue && (editCollections.Any(book => book.Id == item.CollectionId)
                && editTeaching.Any(page => page.Id == item.CollectionId)
                || teachingCollections.Any(book => book.Id == item.CollectionId && (book.CoverId == item.OriginalAssetId
                    || positionedCovers.Any(cover => cover.Id == book.CoverId && cover.ArtworkSourceId == item.OriginalAssetId)))
                && teaching.Any(page => page.Id == item.CollectionId))
            || item.NotionPageId.HasValue && (editTeaching.Any(page => page.Id == item.NotionPageId)
                || teaching.Any(page => page.Id == item.NotionPageId && (item.Kind == Cover
                    ? page.PublishedCoverId == item.OriginalAssetId
                        || positionedCovers.Any(cover => cover.Id == page.PublishedCoverId && cover.ArtworkSourceId == item.OriginalAssetId)
                    : page.PublishedAssetIdsJson.Contains("\"" + item.OriginalAssetId.ToString().ToLower() + "\""))))
            || item.NotebookId.HasValue && readableBooks.Any(book => book.Id == item.NotebookId
                && (book.OwnerSub == subject || book.CoverId == item.OriginalAssetId
                    || positionedCovers.Any(cover => cover.Id == book.CoverId && cover.ArtworkSourceId == item.OriginalAssetId)))
            || item.NotebookPageId.HasValue && (editNotes.Any(note => note.Id == item.NotebookPageId)
                || readableNotes.Any(note => note.Id == item.NotebookPageId && (item.Kind == Cover
                    ? note.CoverId == item.OriginalAssetId
                        || positionedCovers.Any(cover => cover.Id == note.CoverId && cover.ArtworkSourceId == item.OriginalAssetId)
                    : note.AssetIdsJson.Contains("\"" + item.OriginalAssetId.ToString().ToLower() + "\"")))));
    }

    private async Task<Dictionary<(string Kind, Guid Id), SourceView>> LoadSources(ArtRow[] rows, ClaimsPrincipal user,
        CancellationToken ct)
    {
        var sources = new Dictionary<(string, Guid), SourceView>();
        Guid[] Ids(string kind) => rows.Where(row => row.SourceKind == kind).Select(row => row.SourceId).Distinct().ToArray();
        var notion = new NotionAccess(db, courses);
        var editablePages = notion.ManageablePages(user);
        var publishedPages = notion.ReadablePages(user, publishedOnly: true);
        var collections = Ids(Collection);
        if (collections.Length > 0)
        {
            var editableBooks = notion.ManageableCollections(user);
            var books = await notion.ReadablePages(user).Where(page => collections.Contains(page.Id))
                .Join(notion.ReadableCollections(user), page => page.Id, book => book.Id, (page, book) => new
                {
                    book.Id, page.Title, page.PublishedTitle,
                    CanEdit = editableBooks.Any(edit => edit.Id == book.Id) && editablePages.Any(edit => edit.Id == page.Id)
                })
                .ToArrayAsync(ct);
            foreach (var book in books)
                sources[(Collection, book.Id)] = new(book.CanEdit ? book.Title : book.PublishedTitle ?? "Undervisningsnotesbog",
                    null, NotesRoutes.Collection(book.Id) + (book.CanEdit ? "" : "?published=true"));
        }

        var teaching = Ids(NotionPage);
        if (teaching.Length > 0)
        {
            var pages = await notion.ReadablePages(user).Where(page => teaching.Contains(page.Id))
                .Select(page => new
                {
                    page.Id, page.Title, page.PublishedTitle, CanEdit = editablePages.Any(edit => edit.Id == page.Id),
                    CollectionTitle = editablePages.Where(root => root.Id == page.CollectionId).Select(root => root.Title).FirstOrDefault()
                        ?? (page.Collection == null ? null : page.Collection.Title),
                    PublishedCollectionTitle = publishedPages.Where(root => root.Id == page.CollectionId)
                        .Select(root => root.PublishedTitle).FirstOrDefault()
                        ?? (page.Collection != null && !db.NotionPages.Any(root => root.Id == page.CollectionId)
                            ? page.Collection.PublishedTitle : null),
                    HoldTitle = db.Hold.Where(hold => hold.Id == page.HoldId).Select(hold => hold.Name).FirstOrDefault()
                }).ToArrayAsync(ct);
            foreach (var page in pages)
                sources[(NotionPage, page.Id)] = new(
                    (page.CanEdit ? page.CollectionTitle : page.PublishedCollectionTitle) ?? page.HoldTitle ?? "Undervisningsnotesbog",
                    page.CanEdit ? page.Title : page.PublishedTitle,
                    $"/notes/{page.Id:D}" + (page.CanEdit ? "" : "?published=true"));
        }

        var personalBooks = Ids(Notebook);
        var personalPages = Ids(NotebookPage);
        if (personalBooks.Length == 0 && personalPages.Length == 0) return sources;

        var notes = await notebooks.ReadableDocuments(user).Where(note => personalPages.Contains(note.Id))
            .Select(note => new { note.Id, note.NotebookId, note.Title, note.Kind }).ToArrayAsync(ct);
        var allBookIds = personalBooks.Concat(notes.Select(note => note.NotebookId).OfType<Guid>()).Distinct().ToArray();
        var owners = await db.Notebooks.Where(book => allBookIds.Contains(book.Id))
            .Select(book => new
            {
                book.Id, book.OwnerSub,
                Name = db.NotebookProfiles.Where(profile => profile.Sub == book.OwnerSub).Select(profile => profile.DisplayName).FirstOrDefault()
            }).ToDictionaryAsync(book => book.Id, ct);
        var subject = UserClaims.GetSub(user);

        string Title(Guid id) => owners.TryGetValue(id, out var owner) ? owner.OwnerSub == subject ? "Min notesbog"
            : string.IsNullOrWhiteSpace(owner.Name) ? "Personlig notesbog" : $"{owner.Name} · Notesbog" : "Personlig notesbog";

        foreach (var id in personalBooks.Where(owners.ContainsKey))
            sources[(Notebook, id)] = new(Title(id), null, owners[id].OwnerSub == subject ? "/notebook" : $"/notebook?owner={id:D}");
        foreach (var note in notes)
            if (note.NotebookId is Guid book)
                sources[(NotebookPage, note.Id)] = new(Title(book), note.Title, $"/notebook/{note.Id:D}");
            else if (note.Kind == "template")
                sources[(NotebookPage, note.Id)] = new("Logbogsskabeloner", note.Title, $"/teacher/notebook-templates/{note.Id:D}");
        return sources;
    }

    private sealed record ArtRow(Guid Id, string CharacterSlug, string Kind, string SourceKind, Guid SourceId, DateTime CreatedAtUtc);
    private sealed record SourceView(string NotebookTitle, string? PageTitle, string Href);
}
