using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Mercantec.Tech.Contracts.GlobalSearchCategories;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class ReferencesTests
{
    [Fact]
    public async Task Global_search_filters_categories_before_counting_and_paging()
    {
        var quest = new LearningQuest { Id = Guid.NewGuid(), HoldId = hold, Title = "Kategoriprøve A", Published = true };
        db.LearningQuests.AddRange(quest, new() { Id = Guid.NewGuid(), HoldId = hold, Title = "Kategoriprøve B", Published = true });
        db.PollRounds.Add(new() { Id = Guid.NewGuid(), HoldId = hold, Question = "Kategoriprøve", State = "open" });
        await db.SaveChangesAsync();

        var all = await GlobalSearchAs(User(student), "kategoriprøve", All, limit: 1);
        Assert.Equal(3, all.Total);
        Assert.Equal(Polls, Assert.Single(all.Items).Category);

        var first = await GlobalSearchAs(User(student), "kategoriprøve", Assignments, limit: 1);
        var next = await GlobalSearchAs(User(student), "kategoriprøve", Assignments, offset: 1, limit: 1);
        Assert.Equal(2, first.Total);
        Assert.Equal(2, next.Total);
        Assert.Equal($"/quests/{quest.Id:D}?hold={hold:D}", Assert.Single(first.Items).Href);
        Assert.Equal("Kategoriprøve B", Assert.Single(next.Items).Title);

        var badges = await GlobalSearchAs(User(student), "Badgekrav", Badges, limit: 10);
        Assert.NotEmpty(badges.Items);
        Assert.All(badges.Items, hit => Assert.StartsWith("/badges/", hit.Href));
    }

    [Fact]
    public async Task Global_search_people_and_notes_obey_rosters_shares_and_publication()
    {
        var outsiderNotebook = new Notebook { Id = Guid.NewGuid(), OwnerSub = outsider };
        var shared = new NotebookDocument { Id = Guid.NewGuid(), Notebook = outsiderNotebook, Title = "Privat notebooknote om skyen" };
        db.NotebookDocuments.AddRange(shared,
            new() { Id = Guid.NewGuid(), Notebook = outsiderNotebook, Title = "En anden privat notebooknote om skyen" },
            new() { Id = Guid.NewGuid(), NotebookId = notebook.Id, Title = "Privat notebooknote i papirkurven", DeletedAtUtc = DateTimeOffset.UtcNow });
        db.NotebookShares.Add(new() { Id = Guid.NewGuid(), NotebookId = outsiderNotebook.Id, PageId = shared.Id, RecipientSub = student });
        var classmate = Guid.NewGuid();
        var classmateId = Guid.NewGuid();
        db.Memberships.Add(new() { Id = classmateId, HoldId = hold, UserSub = classmate, DisplayName = "Ny klassekammerat" });
        db.ProfileNames.Add(new() { UserSub = classmate, DisplayName = "Klassekammeratens profilnavn" });
        await db.SaveChangesAsync();

        var found = Assert.Single((await GlobalSearchAs(User(student), "profilnavn", People)).Items);
        Assert.Equal($"/notion/holds/{hold:D}/students/{classmateId:D}", found.Href);
        Assert.Empty((await GlobalSearchAs(User(outsider), "profilnavn", People)).Items);
        Assert.Empty((await GlobalSearchAs(User(student), "Anden lærer", People)).Items);
        Assert.Equal($"/people/{teacher:D}", Assert.Single((await GlobalSearchAs(User(student), "Lærer", People)).Items).Href);

        var studentNotes = await GlobalSearchAs(User(student), "privat notebooknote", Notes);
        Assert.Equal($"/notebook/{shared.Id:D}", Assert.Single(studentNotes.Items).Href);
        Assert.Empty((await GlobalSearchAs(User(teacher, true), "privat notebooknote", Notes)).Items);
        Assert.NotEmpty((await GlobalSearchAs(User(teacher, true), "Privat SQL", Notes)).Items);
        Assert.Empty((await GlobalSearchAs(User(student), "hemmelig kladde", Notes)).Items);
        Assert.NotEmpty((await GlobalSearchAs(User(teacher, true), "hemmelig kladde", Notes)).Items);
        Assert.Empty((await GlobalSearchAs(User(teacher, true), "hemmelig kladde", Notes, publishedOnly: true)).Items);
    }

    [Fact]
    public async Task Global_search_limits_uploaded_projects_files_and_submissions_to_authorized_accounts()
    {
        var classmate = Guid.NewGuid();
        db.Memberships.Add(new() { Id = Guid.NewGuid(), HoldId = hold, UserSub = classmate, DisplayName = "Klassekammerat" });
        var ownFile = new FileAsset { Id = Guid.NewGuid(), OwnerSub = student, Key = "student-game", OriginalName = "Spilprojekt elev.zip" };
        var otherFile = new FileAsset { Id = Guid.NewGuid(), OwnerSub = classmate, Key = "classmate-game", OriginalName = "Spilprojekt kammerat.zip" };
        db.UploadedGames.AddRange(new() { Id = Guid.NewGuid(), OwnerSub = student, RequestId = Guid.NewGuid(), Title = "Privat spilprojekt elev", FileAsset = ownFile },
            new() { Id = Guid.NewGuid(), OwnerSub = classmate, RequestId = Guid.NewGuid(), Title = "Privat spilprojekt kammerat", FileAsset = otherFile });
        var quest = new LearningQuest { Id = Guid.NewGuid(), HoldId = hold, Title = "Opgave om et spilprojekt", Published = true };
        var ownSubmission = new QuestSubmission { Id = Guid.NewGuid(), Quest = quest, CreatedBySub = student, OwnerKey = $"user:{student:D}" };
        var otherSubmission = new QuestSubmission { Id = Guid.NewGuid(), Quest = quest, CreatedBySub = classmate, OwnerKey = $"user:{classmate:D}" };
        db.QuestSubmissions.AddRange(ownSubmission, otherSubmission);
        await db.SaveChangesAsync();

        var projects = await GlobalSearchAs(User(student), "Privat spilprojekt", Projects);
        Assert.Equal($"/references/file/file/{ownFile.Id:D}", Assert.Single(projects.Items).Href);
        var all = await GlobalSearchAs(User(student), "spilprojekt elev", All);
        Assert.Equal(Projects, Assert.Single(all.Items).Category);
        Assert.Empty((await GlobalSearchAs(User(teacher, true), "Privat spilprojekt", Projects)).Items);
        var files = await GlobalSearchAs(User(student), "spilprojekt", Files);
        Assert.EndsWith(ownFile.Id.ToString("D"), Assert.Single(files.Items).Href);
        Assert.Empty((await GlobalSearchAs(User(outsider), "spilprojekt", Files)).Items);
        var assignments = await GlobalSearchAs(User(student), "aflevering spilprojekt", Assignments);
        Assert.Equal($"/quests/{quest.Id:D}?hold={hold:D}&submission={ownSubmission.Id:D}", Assert.Single(assignments.Items).Href);
        Assert.Empty((await GlobalSearchAs(User(outsider), "aflevering spilprojekt", Assignments)).Items);
    }

    [Fact]
    public async Task Global_search_rejects_unauthenticated_or_invalid_requests_and_empty_input_returns_no_people()
    {
        Assert.IsType<UnauthorizedResult>((await GlobalController(new()).Search(default, "Lærer")).Result);
        var controller = GlobalController(User(student));
        Assert.IsType<BadRequestObjectResult>((await controller.Search(default, "Lærer", "unknown")).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Search(default, new string('a', 201))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Search(default, "Lærer\0")).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Search(default, "Lærer", limit: 101)).Result);
        Assert.Empty((await GlobalSearchAs(User(student), " \t ", People)).Items);
    }

    [Fact]
    public async Task Global_search_knowledge_checks_and_electives_follow_their_catalog_visibility()
    {
        var ownDraft = new KnowledgeCheckQuiz { Id = Guid.NewGuid(), OwnerSub = teacher, Title = "Søgeøvelse kladde" };
        var visible = new KnowledgeCheckPublication
        {
            Id = Guid.NewGuid(), HoldId = hold, IsActive = true,
            Version = new() { Id = Guid.NewGuid(), Quiz = ownDraft, Title = "Søgeøvelse udgivet", DraftRevision = 1 }
        };
        var unrelated = new KnowledgeCheckPublication
        {
            Id = Guid.NewGuid(), HoldId = otherHold, IsActive = true,
            Version = new() { Id = Guid.NewGuid(), Quiz = ownDraft, Title = "Søgeøvelse for et andet hold", DraftRevision = 2 }
        };
        db.KnowledgeCheckPublications.AddRange(visible, unrelated);
        var catalogItem = new Elective { Id = Guid.NewGuid(), Title = "Søgeøvelse i kataloget", Status = "template" };
        var draftItem = new Elective { Id = Guid.NewGuid(), Title = "Søgeøvelse i en kladde", Status = "draft" };
        db.Electives.AddRange(catalogItem, draftItem);
        await db.SaveChangesAsync();

        var checks = await GlobalSearchAs(User(student), "søgeøvelse", GlobalSearchCategories.KnowledgeChecks);
        Assert.Equal($"/videnstjek/{visible.Id:D}", Assert.Single(checks.Items).Href);
        Assert.Contains((await GlobalSearchAs(User(teacher, true), "søgeøvelse", GlobalSearchCategories.KnowledgeChecks)).Items,
            hit => hit.Href == $"/videnstjek/rediger/{ownDraft.Id:D}");

        var electives = await GlobalSearchAs(User(student), "søgeøvelse", Electives);
        Assert.Equal($"/valgfag?vis=katalog&id={catalogItem.Id:D}", Assert.Single(electives.Items).Href);
        Assert.Contains((await GlobalSearchAs(User(teacher, true), "søgeøvelse", Electives)).Items,
            hit => hit.Href == $"/valgfag?vis=administrer&id={draftItem.Id:D}");
    }

    private async Task<GlobalSearchResult> GlobalSearchAs(ClaimsPrincipal user, string query, string category,
        int offset = 0, int limit = 30, bool publishedOnly = false)
    {
        var result = await GlobalController(user).Search(default, query, category, offset, limit, publishedOnly);
        return Assert.IsType<GlobalSearchResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private SearchController GlobalController(ClaimsPrincipal user) => new(db, TimeProvider.System)
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = user } }
    };
}
