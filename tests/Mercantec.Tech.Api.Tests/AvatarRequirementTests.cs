using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Avatars;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class AvatarRequirementTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"avatar-requirements-{Guid.NewGuid():N}.db");
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000201");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000202");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000203");
    private static readonly Guid Dan = Guid.Parse("00000000-0000-0000-0000-000000000204");
    private static readonly Guid Eve = Guid.Parse("00000000-0000-0000-0000-000000000205");
    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_path};Pooling=False").Options);
    public async Task InitializeAsync() { await using var db = Open(); await db.Database.EnsureCreatedAsync(); }
    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(_path + suffix);
        return Task.CompletedTask;
    }
    private static ControllerContext Context(Guid subject, bool teacher = false) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                teacher ? [new("sub", subject.ToString()), new("role", "Underviser")] : [new("sub", subject.ToString())], "test"))
        }
    };
    private static AvatarsController As(AppDbContext db, Guid subject, bool teacher = false) =>
        new(db) { ControllerContext = Context(subject, teacher) };
    private static AvatarProfileDto Ok(ActionResult<AvatarProfileDto> result) => Assert.IsType<AvatarProfileDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    private static AvatarUnlockProgressDto Progress(AvatarProfileDto profile) => Assert.Single(profile.UnlockProgress!, p => p.AvatarId == "santa" && p.RequirementId == "christmas-gift-recipients");
    private static AvatarUnlockProgressDto BilboProgress(AvatarProfileDto profile) =>
        Assert.Single(profile.UnlockProgress!, progress => progress.AvatarId == AvatarEconomy.BilboId);
    private static T Read<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private static void Missing(ActionResult<AvatarProfileDto> result)
    {
        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal("avatar_requirement_not_met", Assert.IsType<ProblemDetails>(response.Value).Extensions["code"]);
    }
    private static ChristmasGift Gift(Guid sender, Guid recipient, string theme = "christmas", bool opened = false, DateTime? created = null) => new()
    {
        Id = Guid.NewGuid(), SenderSub = sender, RecipientSub = recipient, Theme = theme,
        SenderName = "Afsender", RecipientName = "Modtager", CreatedAtUtc = created ?? DateTime.UtcNow,
        OpenedAtUtc = opened ? DateTime.UtcNow : null
    };

    [Fact]
    public async Task MissingGiftsRejectUnlockWithoutChangingWalletSelectionRevisionOrImport()
    {
        await using var db = Open();
        var controller = As(db, Alice);
        var before = Ok(await controller.Get(default));
        Assert.Equal(0, Progress(before).Current); Assert.Equal(3, Progress(before).Required); Assert.False(Progress(before).IsMet);
        Missing(await controller.Unlock("santa", default));
        var after = Ok(await controller.Get(default));
        Assert.Equal(before.Gold, after.Gold); Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.SelectedAvatarId, after.SelectedAvatarId); Assert.Equal(before.CanImportLegacy, after.CanImportLegacy);
        Assert.DoesNotContain("santa", after.OwnedAvatarIds);
    }

    [Fact]
    public async Task OnlyThreeDistinctOutgoingChristmasRecipientsQualifyAndUnopenedGiftsCount()
    {
        await using var db = Open();
        db.ChristmasGifts.AddRange(
            Gift(Alice, Bob), Gift(Alice, Bob, opened: true), Gift(Alice, Carol),
            Gift(Alice, Dan, "newyear"), Gift(Alice, Eve, "unknown"), Gift(Bob, Dan), Gift(Carol, Alice), Gift(Alice, Guid.Empty));
        await db.SaveChangesAsync();
        var controller = As(db, Alice);
        Assert.Equal(2, Progress(Ok(await controller.Get(default))).Current);
        Missing(await controller.Unlock("santa", default));
        db.ChristmasGifts.Add(Gift(Alice, Dan)); await db.SaveChangesAsync();
        var complete = Ok(await controller.Get(default));
        Assert.Equal(3, Progress(complete).Current); Assert.True(Progress(complete).IsMet);
        var purchased = Ok(await controller.Unlock("santa", default));
        Assert.Equal(170, purchased.Gold); Assert.Equal("santa", purchased.SelectedAvatarId);
        Assert.Equal(purchased.Revision, Ok(await controller.Unlock("santa", default)).Revision);
        Assert.Single(await db.AvatarOwnerships.Where(x => x.UserSub == Alice && x.AvatarId == "santa").ToListAsync());
    }

    [Fact]
    public async Task CountsAllHistoryRatherThanTheFiftyGiftMailboxWindowAndPersistsAcrossDevices()
    {
        await using (var first = Open())
        {
            first.ChristmasGifts.AddRange(Gift(Alice, Carol, created: DateTime.UtcNow.AddDays(-3)), Gift(Alice, Dan, created: DateTime.UtcNow.AddDays(-2)));
            for (var i = 0; i < 55; i++) first.ChristmasGifts.Add(Gift(Alice, Bob));
            await first.SaveChangesAsync();
            Assert.Equal(3, Progress(Ok(await As(first, Alice).Get(default))).Current);
        }
        await using var second = Open();
        var actual = Ok(await As(second, Alice).Get(default)); Assert.True(Progress(actual).IsMet);
        Assert.Equal(0, Progress(Ok(await As(second, Eve).Get(default))).Current);
        Assert.Equal("santa", Ok(await As(second, Alice).Unlock("santa", default)).SelectedAvatarId);
    }

    [Fact]
    public async Task SuccessfulGiftApiSendsAdvanceTheRequirementButFailuresAndRetriesDoNot()
    {
        await using var db = Open();
        db.GiftContacts.AddRange(new GiftContact { UserSub = Alice, DisplayName = "Afsender" },
            new GiftContact { UserSub = Bob, DisplayName = "B" }, new GiftContact { UserSub = Carol, DisplayName = "C" }, new GiftContact { UserSub = Dan, DisplayName = "D" });
        await db.SaveChangesAsync();
        var gifts = new GiftsController(db) { ControllerContext = Context(Alice, teacher: true) };
        var draft = new GiftsController.SendGiftRequest(Guid.NewGuid(), Bob, "cheer", "berry", "Glædelig jul", "christmas");
        Assert.IsType<CreatedResult>(await gifts.Send(draft, default));
        Assert.IsType<OkObjectResult>(await gifts.Send(draft, default));
        Assert.IsType<BadRequestObjectResult>(await gifts.Send(draft with { Id = Guid.NewGuid(), RecipientSub = Alice }, default));
        Assert.IsType<BadRequestObjectResult>(await gifts.Send(draft with { Id = Guid.NewGuid(), RecipientSub = Eve }, default));
        Assert.Equal(1, Progress(Ok(await As(db, Alice).Get(default))).Current);
        Assert.IsType<CreatedResult>(await gifts.Send(draft with { Id = Guid.NewGuid(), RecipientSub = Carol }, default));
        Missing(await As(db, Alice).Unlock("santa", default));
        Assert.IsType<CreatedResult>(await gifts.Send(draft with { Id = Guid.NewGuid(), RecipientSub = Dan }, default));
        Assert.True(Progress(Ok(await As(db, Alice).Get(default))).IsMet);
    }

    [Fact]
    public async Task PreviouslyOwnedSantaRemainsPermanentlySelectableWithoutRetrospectiveGiftClaims()
    {
        await using var db = Open();
        db.AvatarProfiles.Add(new AvatarProfile
        {
            UserSub = Alice, SelectedAvatarId = "santa", Gold = 170, LegacyImportAvailable = false,
            Avatars = [new() { UserSub = Alice, AvatarId = "starter" }, new() { UserSub = Alice, AvatarId = "santa" }]
        });
        await db.SaveChangesAsync();
        var controller = As(db, Alice);
        var existing = Ok(await controller.Get(default)); Assert.False(Progress(existing).IsMet);
        Ok(await controller.Select(new("starter"), default));
        var restored = Ok(await controller.Select(new("santa"), default));
        Assert.Equal("santa", restored.SelectedAvatarId); Assert.Equal(170, restored.Gold);
        var retry = Ok(await controller.Unlock("santa", default)); Assert.Equal(restored.Revision, retry.Revision);
    }

    [Fact]
    public async Task TeacherUnlocksPaidAndTaskLockedAvatarsForFreeAndKeepsThemOnTheAccount()
    {
        await using var db = Open();
        var teacher = As(db, Alice, teacher: true);
        Assert.True(Ok(await teacher.Get(default)).CanUnlockAvatarsForFree);
        var stored = await db.AvatarProfiles.SingleAsync(profile => profile.UserSub == Alice);
        stored.Level = 1;
        stored.Gold = 0;
        await db.SaveChangesAsync();

        var santa = Ok(await teacher.Unlock(AvatarEconomy.SantaId, default));
        Assert.True(santa.CanUnlockAvatarsForFree);
        Assert.False(Progress(santa).IsMet);
        Assert.Equal(1, santa.Level);
        Assert.Equal(0, santa.Gold);
        Assert.Equal(AvatarEconomy.SantaId, santa.SelectedAvatarId);
        Assert.Equal(santa.Revision, Ok(await teacher.Unlock(AvatarEconomy.SantaId, default)).Revision);

        var bilbo = Ok(await teacher.Unlock(AvatarEconomy.BilboId, default));
        Assert.False(BilboProgress(bilbo).IsMet);
        Assert.Equal(AvatarEconomy.BilboId, bilbo.SelectedAvatarId);
        Assert.Equal(0, bilbo.Gold);

        await using var secondDevice = Open();
        var saved = Ok(await As(secondDevice, Alice).Get(default));
        Assert.False(saved.CanUnlockAvatarsForFree);
        Assert.Equal(AvatarEconomy.BilboId, saved.SelectedAvatarId);
        Assert.Contains(AvatarEconomy.SantaId, saved.OwnedAvatarIds);
        Assert.Contains(AvatarEconomy.BilboId, saved.OwnedAvatarIds);
        Assert.Equal(0, saved.Gold);

        var student = As(secondDevice, Bob);
        Assert.False(Ok(await student.Get(default)).CanUnlockAvatarsForFree);
        Missing(await student.Unlock(AvatarEconomy.SantaId, default));
        Missing(await student.Unlock(AvatarEconomy.BilboId, default));
    }

    [Fact]
    public async Task BilboRequiresThreeDistinctSubmittedBackendChecksAndUnlocksWithoutSpendingGold()
    {
        await using var db = Open();
        var teacher = new KnowledgeChecksController(db, new CourseAccess(db)) { ControllerContext = Context(Eve, teacher: true) };
        var alice = new KnowledgeChecksController(db, new CourseAccess(db)) { ControllerContext = Context(Alice) };
        var bob = new KnowledgeChecksController(db, new CourseAccess(db)) { ControllerContext = Context(Bob) };
        var avatars = As(db, Alice);
        var question = new KnowledgeCheckQuestion(Guid.NewGuid(), "single", "Vælg en svarmulighed",
            ["Første", "Anden"], [0], [], false, "En forklaring", null);

        async Task<KnowledgeCheckPublicationView> Publish(string title, string topic)
        {
            var draft = Read<KnowledgeCheckDraftView>(await teacher.Create(new(title, "", topic, null, [question]), default));
            return Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, new(null, null, "after-submit"), default));
        }

        async Task<KnowledgeCheckAttemptView> Submit(KnowledgeChecksController learner, Guid publicationId, int answer = 0)
        {
            var attempt = Read<KnowledgeCheckAttemptView>(await learner.StartAttempt(publicationId, new(), null, null, default));
            var saved = Read<KnowledgeCheckAttemptView>(await learner.SaveAttempt(publicationId, attempt.Id,
                new(attempt.Revision, 0, [new(question.Id, [answer], null)]), null, null, default));
            return Read<KnowledgeCheckAttemptView>(await learner.SubmitAttempt(publicationId, saved.Id, new(saved.Revision), null, null, default));
        }

        var initial = Ok(await avatars.Get(default));
        Assert.Equal(0, BilboProgress(initial).Current);
        Missing(await avatars.Unlock(AvatarEconomy.BilboId, default));
        var first = await Publish("API og HTTP", "Backend");
        var duplicatePublication = Read<KnowledgeCheckPublicationView>(
            await teacher.Publish(first.QuizId, new(null, null, "after-submit"), default));
        Assert.NotEqual(first.Id, duplicatePublication.Id);
        Assert.Equal(0, (await Submit(alice, first.Id, answer: 1)).Score);
        await Submit(alice, duplicatePublication.Id);
        await teacher.ClosePublication(first.Id, default);
        await Submit(alice, (await Publish("DTO'er", "backend")).Id);
        await Submit(alice, (await Publish("Frontend", "Frontend")).Id);
        await teacher.ImportLegacy(default);
        var legacyId = LegacyKnowledgeCheckCatalog.ForOwner(Eve).Single(entry => entry.Value.Key == "blazor-signalr").Key;
        var legacy = Read<KnowledgeCheckDraftView>(await teacher.GetDraft(legacyId, default));
        await teacher.Update(legacyId, new(legacy.Title, legacy.Description, legacy.Topic, null, [question], legacy.Revision), default);
        var third = Read<KnowledgeCheckPublicationView>(await teacher.Publish(legacyId, new(null, null, "after-submit"), default));
        Assert.Equal("Frontend", third.Topic);
        var practice = Read<KnowledgeCheckIndex>(await alice.List(default)).Publications.Single(item => item.Id == third.Id);
        Assert.Contains("Backend", Assert.IsType<KnowledgeCheckLegacyDiscovery>(practice.LegacyDiscovery).Categories);
        await Submit(bob, third.Id);
        var started = Read<KnowledgeCheckAttemptView>(await alice.StartAttempt(third.Id, new(), null, null, default));
        Assert.IsType<ConflictObjectResult>(await alice.SubmitAttempt(third.Id, started.Id, new(started.Revision), null, null, default));
        var beforeClaim = Ok(await avatars.Get(default));
        Assert.Equal(2, BilboProgress(beforeClaim).Current);
        Missing(await avatars.Unlock(AvatarEconomy.BilboId, default));
        var unchanged = Ok(await avatars.Get(default));
        Assert.Equal(initial.Gold, unchanged.Gold);
        Assert.Equal(initial.Revision, unchanged.Revision);
        Assert.Equal(initial.SelectedAvatarId, unchanged.SelectedAvatarId);

        await Submit(alice, third.Id);
        var qualified = BilboProgress(Ok(await avatars.Get(default)));
        Assert.Equal(3, qualified.Current);
        Assert.True(qualified.IsMet);
        var claimed = Ok(await avatars.Unlock(AvatarEconomy.BilboId, default));
        Assert.Equal(initial.Gold, claimed.Gold);
        Assert.Equal(AvatarEconomy.BilboId, claimed.SelectedAvatarId);

        await using var secondDevice = Open();
        var persisted = Ok(await As(secondDevice, Alice).Get(default));
        Assert.Equal(AvatarEconomy.BilboId, persisted.SelectedAvatarId);
        Assert.Contains(AvatarEconomy.BilboId, persisted.OwnedAvatarIds);
        Assert.Equal(persisted.Revision, Ok(await As(secondDevice, Alice).Unlock(AvatarEconomy.BilboId, default)).Revision);
        Assert.Equal(1, BilboProgress(Ok(await As(secondDevice, Bob).Get(default))).Current);
        Missing(await As(secondDevice, Bob).Unlock(AvatarEconomy.BilboId, default));
    }

    [Fact]
    public async Task GenericEvaluatorSupportsAnotherThemeAndFailsClosedForUnknownKindsOrTargets()
    {
        await using var db = Open();
        db.ChristmasGifts.AddRange(Gift(Alice, Bob, "newyear"), Gift(Alice, Carol, "newyear")); await db.SaveChangesAsync();
        AvatarUnlockDefinition[] catalog =
        [
            new("future", "Fremtidig", 1, 0, Requirements:
            [
                new("known", AvatarRequirementKinds.SentGiftsDistinctRecipients, "newyear", 2, "To nytårsgaver"),
                new("kind", "unsupported_metric", "newyear", 0, "Ukendt krav"),
                new("target", AvatarRequirementKinds.SentGiftsDistinctRecipients, "unknown", 1, "Ukendt tema")
            ])
        ];
        var progress = await AvatarRequirementEvaluator.EvaluateAsync(db, Alice, catalog, default);
        Assert.True(Assert.Single(progress, x => x.RequirementId == "known").IsMet);
        Assert.False(Assert.Single(progress, x => x.RequirementId == "kind").IsMet);
        Assert.False(Assert.Single(progress, x => x.RequirementId == "target").IsMet);
    }

    [Fact]
    public async Task GiftThemeIsIncludedInTheSignedInUsersPrivacyExport()
    {
        await using var db = Open(); db.ChristmasGifts.Add(Gift(Alice, Bob)); await db.SaveChangesAsync();
        var controller = new PrivacyController(db) { ControllerContext = Context(Alice) };
        var file = Assert.IsType<FileContentResult>(await controller.Export(default));
        using var export = JsonDocument.Parse(file.FileContents);
        var gift = Assert.Single(export.RootElement.GetProperty("PlatformRecords").GetProperty("gifts").EnumerateArray());
        Assert.Equal("christmas", gift.GetProperty("Theme").GetString());
    }
}
