using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class HoldCommandCenterTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId, otherHoldId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "Programmering", CreatedBySub = teacher };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3", Forloeb = course };
        var other = new Hold { Id = Guid.NewGuid(), Name = "H4", Forloeb = new() { Id = Guid.NewGuid(), Name = "Andet", CreatedBySub = outsider } };
        holdId = hold.Id; otherHoldId = other.Id;
        db.Hold.Add(other);
        db.Memberships.Add(new() { Id = Guid.NewGuid(), Hold = hold, UserSub = student, DisplayName = "Ada" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private T As<T>(T controller, Guid subject, bool isTeacher = false) where T : ControllerBase
    {
        var claims = new List<Claim> { new("sub", subject.ToString()) };
        if (isTeacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }
    private HoldCalendarController Calendar(Guid subject, bool isTeacher = false) => As(new HoldCalendarController(db, new(db)), subject, isTeacher);
    private ActionCenterController Actions(Guid subject) => As(new ActionCenterController(db, new(db)), subject);
    private QuestsController Quests(Guid subject, bool isTeacher = false) => As(new QuestsController(db, new(db)), subject, isTeacher);
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private SaveHoldCalendarEvent Event() => new(holdId, "Workshop", "Byg spil", new(2026, 11, 5, 8, 0, 0, TimeSpan.FromHours(1)), new(2026, 11, 5, 10, 0, 0, TimeSpan.FromHours(1)), "204", "workshop");

    [Theory]
    [InlineData("main")]
    [InlineData("daily")]
    [InlineData("side")]
    public async Task QuestKindsPersistAndAreReadableThroughDeepLinks(string kind)
    {
        var quest = Value<QuestView>(await Quests(teacher, true).Create(new(holdId, "Byg en løsning", "Aflevér kode", "individual", true, null, kind), default));
        db.ChangeTracker.Clear();
        var visible = Value<QuestView>(await Quests(student).Get(quest.Id, default));
        Assert.Equal(kind, visible.Kind);
        Assert.IsType<NotFoundResult>(await Quests(outsider).Get(quest.Id, default));
    }

    [Fact]
    public async Task InvalidQuestKindAndStudentCreationAreRejected()
    {
        var invalid = new SaveQuest(holdId, "Quest", "Opgave", "individual", true, null, "unknown");
        Assert.IsType<BadRequestObjectResult>(await Quests(teacher, true).Create(invalid, default));
        Assert.IsType<ForbidResult>(await Quests(student).Create(invalid with { Kind = "main" }, default));
        Assert.Empty(await db.LearningQuests.ToListAsync());
    }

    [Fact]
    public async Task DraftQuestsAreOnlyVisibleToManagingTeachers()
    {
        var quest = Value<QuestView>(await Quests(teacher, true).Create(new(holdId, "Kladde", "", "group", false, null, "side"), default));
        Assert.IsType<NotFoundResult>(await Quests(student).Get(quest.Id, default));
        Assert.Equal("side", Value<QuestView>(await Quests(teacher, true).Get(quest.Id, default)).Kind);
    }

    [Fact]
    public async Task CalendarIsHoldScopedAndStudentReadOnly()
    {
        var item = Value<HoldCalendarEventView>(await Calendar(teacher, true).Create(holdId, Event(), default));
        Assert.Equal(TimeSpan.Zero, item.StartUtc.Offset);
        Assert.Equal(7, item.StartUtc.Hour);
        Assert.Single(Value<HoldCalendarEventView[]>(await Calendar(student).List(holdId, default)));
        Assert.IsType<NotFoundResult>(await Calendar(outsider).List(holdId, default));
        Assert.IsType<NotFoundResult>(await Calendar(student).Create(holdId, Event(), default));
        Assert.IsType<NotFoundResult>(await Calendar(student).Update(holdId, item.Id, Event(), default));
        Assert.IsType<NotFoundResult>(await Calendar(student).Delete(holdId, item.Id, 0, default));
        Assert.IsType<NotFoundResult>(await Calendar(teacher, true).Create(otherHoldId, Event() with { HoldId = otherHoldId }, default));
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, Event() with { HoldId = otherHoldId }, default));
    }

    [Fact]
    public async Task CalendarRejectsInvalidTimesAndUnknownKinds()
    {
        var request = Event();
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, request with { EndUtc = request.StartUtc }, default));
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, request with { EndUtc = request.StartUtc.AddDays(32) }, default));
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, request with { Kind = "unknown" }, default));
        Assert.Empty(await db.HoldCalendarEvents.ToListAsync());
    }

    [Fact]
    public async Task CalendarUpdateAndDeleteRejectStaleVersions()
    {
        var item = Value<HoldCalendarEventView>(await Calendar(teacher, true).Create(holdId, Event(), default));
        var updated = Value<HoldCalendarEventView>(await Calendar(teacher, true).Update(holdId, item.Id, Event() with { Title = "Nyt navn", Version = item.Version }, default));
        Assert.Equal(item.Version + 1, updated.Version);
        Assert.IsType<ConflictObjectResult>(await Calendar(teacher, true).Update(holdId, item.Id, Event() with { Version = item.Version }, default));
        Assert.IsType<ConflictObjectResult>(await Calendar(teacher, true).Delete(holdId, item.Id, item.Version, default));
        Assert.IsType<OkObjectResult>(await Calendar(teacher, true).Delete(holdId, item.Id, updated.Version, default));
        Assert.Empty(Value<HoldCalendarEventView[]>(await Calendar(student).List(holdId, default)));
    }

    [Fact]
    public async Task PersonalCalendarCombinesOnlyTheRequestedDatesAndAccessibleHolds()
    {
        var request = Event();
        db.Memberships.Add(new() { Id = Guid.NewGuid(), HoldId = otherHoldId, UserSub = student, DisplayName = "Ada" });
        var published = AddQuest("Frist på H3", holdId, request.EndUtc, published: true);
        var other = AddQuest("Frist på H4", otherHoldId, request.EndUtc, published: true);
        AddQuest("Kladdefrist", holdId, request.EndUtc, published: false);
        AddQuest("Uden for perioden", holdId, request.EndUtc.AddDays(1), published: true);
        var restricted = AddQuest("Kun tildelte elever", holdId, request.EndUtc, published: true);
        restricted.AudienceMode = "students";
        await db.SaveChangesAsync();

        var startsEarlier = Value<HoldCalendarEventView>(await Calendar(teacher, true).Create(holdId,
            request with { StartUtc = request.StartUtc.AddHours(-1), AttendanceMode = "online" }, default));
        var startsAtEnd = request.EndUtc.AddHours(1);
        await Calendar(teacher, true).Create(holdId, request with { StartUtc = startsAtEnd, EndUtc = startsAtEnd.AddHours(1) }, default);
        var otherEvent = Value<HoldCalendarEventView>(await Calendar(outsider, true).Create(otherHoldId,
            request with { HoldId = otherHoldId }, default));

        var view = Value<HoldCalendarOverview>(await Calendar(student).Overview(request.StartUtc, startsAtEnd, null, default));
        Assert.Equal(new[] { holdId, otherHoldId }, view.Holds.Select(h => h.HoldId));
        Assert.All(view.Holds, h => Assert.False(h.CanManage));
        Assert.Equal(new[] { startsEarlier.Id, otherEvent.Id }, view.Events.Select(e => e.Id));
        Assert.Equal("online", view.Events[0].AttendanceMode);
        Assert.Equal(new[] { published.Id, other.Id }.Order(), view.Deadlines.Select(d => d.QuestId).Order());
        Assert.Empty(view.QuestChoices);

        var forTeacher = Value<HoldCalendarOverview>(await Calendar(teacher, true).Overview(request.StartUtc, startsAtEnd, null, default));
        Assert.True(Assert.Single(forTeacher.Holds).CanManage);
        Assert.Contains(forTeacher.Deadlines, d => d.QuestId == restricted.Id);
        Assert.Contains(forTeacher.QuestChoices, q => q.QuestId == published.Id);
        var preview = Value<HoldCalendarOverview>(await Calendar(teacher, true).Overview(request.StartUtc, startsAtEnd, student, default));
        Assert.False(Assert.Single(preview.Holds).CanManage);
        Assert.Equal(published.Id, Assert.Single(preview.Deadlines).QuestId);
        Assert.Empty(preview.QuestChoices);
        var pupilQuests = Value<QuestView[]>(await Quests(teacher, true).List(holdId, default, student));
        Assert.Contains(pupilQuests, q => q.Id == published.Id && !q.CanManage);
        Assert.DoesNotContain(pupilQuests, q => q.Id == restricted.Id || !q.Published);
        Assert.IsType<NotFoundResult>(await Quests(teacher, true).List(otherHoldId, default, student));
        Assert.IsType<ForbidResult>(await Quests(student).List(holdId, default, outsider));
        Assert.IsType<ForbidResult>(await Calendar(student).Overview(request.StartUtc, startsAtEnd, outsider, default));
        Assert.IsType<BadRequestObjectResult>(await Calendar(student).Overview(request.StartUtc, request.StartUtc.AddDays(46), null, default));
    }

    [Fact]
    public async Task AQuestBlockFollowsTheCurrentAudienceAndPublication()
    {
        var request = Event();
        var member = await db.Memberships.SingleAsync(m => m.UserSub == student);
        db.Memberships.Add(new() { Id = Guid.NewGuid(), HoldId = holdId, UserSub = outsider, DisplayName = "Bo" });
        var selected = AddQuest("Holdets individuelle quest", holdId, request.EndUtc, published: true);
        selected.AudienceMode = "students";
        var assignment = new QuestAudienceMembership { QuestId = selected.Id, MembershipId = member.Id };
        db.QuestAudienceMemberships.Add(assignment);
        var differentHold = AddQuest("Krydsreference til H4", otherHoldId, request.EndUtc, published: true);
        await db.SaveChangesAsync();

        var block = request with { Kind = "quest", QuestId = selected.Id, AttendanceMode = "home" };
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, block with { QuestId = differentHold.Id }, default));
        Assert.IsType<BadRequestObjectResult>(await Calendar(teacher, true).Create(holdId, block with { AttendanceMode = "unknown" }, default));
        var saved = Value<HoldCalendarEventView>(await Calendar(teacher, true).Create(holdId, block, default));
        Assert.Equal(selected.Id, saved.QuestId);
        Assert.Equal("home", saved.AttendanceMode);
        Assert.Equal(saved.Id, Assert.Single(Value<HoldCalendarEventView[]>(await Calendar(student).List(holdId, default))).Id);
        Assert.Empty(Value<HoldCalendarEventView[]>(await Calendar(outsider).List(holdId, default)));
        var moved = block with { StartUtc = block.StartUtc.AddMinutes(10), EndUtc = block.EndUtc.AddMinutes(10), Version = saved.Version };
        Assert.IsType<OkObjectResult>(await Calendar(teacher, true).Update(holdId, saved.Id, moved, default));
        Assert.Equal(saved.Id, Assert.Single(await Notices(student)).TargetId);
        Assert.Empty(await Notices(outsider));

        selected.Published = false;
        await db.SaveChangesAsync();
        var unpublished = Value<HoldCalendarOverview>(await Calendar(student).Overview(request.StartUtc, request.EndUtc.AddHours(1), null, default));
        Assert.Empty(unpublished.Events);
        Assert.Empty(unpublished.Deadlines);
        Assert.Empty(await Notices(student));
        selected.Published = true;
        db.QuestAudienceMemberships.Remove(assignment);
        db.Memberships.Remove(member);
        await db.SaveChangesAsync();
        var removed = Value<HoldCalendarOverview>(await Calendar(student).Overview(request.StartUtc, request.EndUtc.AddHours(1), null, default));
        Assert.Empty(removed.Holds);
        Assert.Empty(removed.Events);
        Assert.Empty(removed.Deadlines);
        Assert.Empty(await Notices(student));
    }

    [Fact]
    public async Task CalendarNoticeTracksTheLatestRealChangeAndCanBeAcknowledgedOnce()
    {
        var request = Event();
        var staff = Calendar(teacher, true);
        var created = Value<HoldCalendarEventView>(await staff.Create(holdId, request, default));
        var renamed = Value<HoldCalendarEventView>(await staff.Update(holdId, created.Id,
            request with { Title = "Ny titel", Version = created.Version }, default));
        Assert.Empty(await Notices(student));

        var move = request with { Title = renamed.Title, StartUtc = request.StartUtc.AddHours(1), EndUtc = request.EndUtc.AddHours(1), Version = renamed.Version };
        var moved = Value<HoldCalendarEventView>(await staff.Update(holdId, created.Id, move, default));
        Assert.IsType<ConflictObjectResult>(await staff.Update(holdId, created.Id, move, default));
        var moveNotice = Assert.Single(await Notices(student));
        Assert.Equal("moved", moveNotice.State);
        Assert.Equal(created.Id, moveNotice.TargetId);
        Assert.Empty(await Notices(outsider));

        Assert.IsType<OkObjectResult>(await staff.Delete(holdId, created.Id, moved.Version, default));
        Assert.IsType<NotFoundResult>(await staff.Delete(holdId, created.Id, moved.Version, default));
        var cancellation = Assert.Single(await Notices(student));
        Assert.Equal("cancelled", cancellation.State);
        Assert.NotEqual(moveNotice.ResourceId, cancellation.ResourceId);
        var read = new ActionCenterReadRequest([new("calendar-change", cancellation.ResourceId)]);
        Assert.IsType<NoContentResult>(await Actions(student).Read(read, default));
        Assert.IsType<NoContentResult>(await Actions(student).Read(read, default));
        Assert.Empty(await Notices(student));
        Assert.Equal(2, await db.HoldCalendarChanges.CountAsync());
        Assert.Single(await db.ActionReadReceipts.ToArrayAsync());
    }

    private LearningQuest AddQuest(string title, Guid hold, DateTimeOffset deadline, bool published)
    {
        var quest = new LearningQuest { Id = Guid.NewGuid(), HoldId = hold, Title = title, DeadlineUtc = deadline.ToUniversalTime(), Published = published };
        db.LearningQuests.Add(quest);
        return quest;
    }

    private async Task<ActionCenterItem[]> Notices(Guid user) => Value<ActionCenterSnapshot>(await Actions(user).Get(null, default, 2))
        .Items.Where(i => i.Kind == "calendar-change").ToArray();
}
