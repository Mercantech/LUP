using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Mercantec.Tech.Api.Tests;

public sealed class LiveCourseTests : IAsyncLifetime
{
    private const long Revision = 7;
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly Clock clock = new();
    private readonly ProjectionHandler projection = new();
    private readonly Guid teacher = Guid.NewGuid(), pupil = Guid.NewGuid(), peer = Guid.NewGuid(), outsider = Guid.NewGuid();
    private readonly Guid pageId = Guid.NewGuid(), parentId = Guid.NewGuid(), holdId = Guid.NewGuid();
    private IConfiguration config = null!;
    private AppDbContext db = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var forloeb = new Forloeb { Id = Guid.NewGuid(), Name = "Software", CreatedBySub = teacher, CreatedAtUtc = clock.Now };
        var hold = new Hold { Id = holdId, Forloeb = forloeb, Name = "Webudvikling", CreatedAtUtc = clock.Now };
        db.Hold.Add(hold);
        db.Memberships.AddRange(
            new Membership { Id = Guid.NewGuid(), HoldId = holdId, UserSub = pupil, Source = "manual" },
            new Membership { Id = Guid.NewGuid(), HoldId = holdId, UserSub = peer, Source = "manual" });
        db.NotionPages.AddRange(Page(parentId, null), Page(pageId, parentId));
        await db.SaveChangesAsync();
        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notion:ServiceSecret"] = new string('a', 48),
        }).Build();
    }

    [Fact]
    public async Task StartUsesCurrentPublishedBytesAndCourseAccessAndBindsExactSlideAndVersion()
    {
        var publishedCourse = await AddCourse(parentId, published: true);
        var hiddenCourse = await AddCourse(pageId, published: false);
        var module = await Save("poll-block", "poll", options: ["Ja", "Nej"], slideIndex: 1);
        var unsavedVersion = await Save("poll-block", "poll", options: ["Grøn", "Rød"], bind: false);
        Assert.IsType<NotFoundResult>((await As(pupil).Start(pageId, new(Revision, publishedCourse), default)).Result);
        Assert.IsType<NotFoundResult>((await As(teacher).Start(pageId, new(Revision, hiddenCourse), default)).Result);
        Assert.IsType<ConflictObjectResult>((await As(teacher).Start(pageId, new(Revision - 1), default)).Result);
        var session = Session(await As(teacher).Start(pageId, new(Revision), default));
        Assert.Equal(publishedCourse, (await db.LiveCourseSessions.AsNoTracking().SingleAsync()).CourseId);
        Assert.Equal(1, projection.Calls);
        Assert.Equal("AQID", projection.PublishedBytes);
        Assert.False(Session(await As(pupil).Get(pageId, default)).MayManage);
        Assert.IsType<NotFoundResult>((await As(outsider).Get(pageId, default)).Result);
        Assert.IsType<NotFoundResult>((await As(outsider, teacherRole: true).Get(pageId, default)).Result);
        Assert.IsType<NotFoundResult>((await As(pupil).OpenRound(pageId, session.Id, new(module.Id, module.BlockId), default)).Result);
        Assert.IsType<NotFoundResult>((await As(teacher).OpenRound(pageId, session.Id, new(module.Id, module.BlockId), default)).Result);
        Assert.Equal(1, Session(await As(teacher).Slide(pageId, session.Id, new(1), default)).SlideIndex);
        Assert.IsType<NotFoundResult>((await As(teacher).OpenRound(pageId, session.Id, new(unsavedVersion.Id, module.BlockId), default)).Result);
        Assert.NotNull(Session(await As(teacher).OpenRound(pageId, session.Id, new(module.Id, module.BlockId), default)).Round);
        Assert.Equal(1, projection.Calls);

        await db.NotionPages.Where(p => p.Id == pageId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PublicationVersion, p => p.PublicationVersion + 1));
        Assert.Null(Ok(await As(pupil).Get(pageId, default)).Session);
        Assert.IsType<ConflictObjectResult>((await As(teacher).Slide(pageId, session.Id, new(0), default)).Result);
        var restarted = Session(await As(teacher).Start(pageId, new(Revision, publishedCourse), default));
        Assert.NotEqual(session.Id, restarted.Id);
    }

    [Fact]
    public async Task RoundResultsAreHiddenAndReconnectRecoversOwnAnswerWithoutMixingRounds()
    {
        var mood = await Save("mood", "poll", pollType: "mood");
        Assert.Equal(["Glad", "Klar", "Midt imellem", "Træt", "Presset"], mood.Options);
        var scale = await Save("scale", "poll", pollType: "scale");
        Assert.Equal(["1", "2", "3", "4", "5"], scale.Options);
        var session = await Start();
        Assert.Equal(StatusCodes.Status304NotModified, Assert.IsType<StatusCodeResult>(
            (await As(pupil).Get(pageId, default, session.Id, session.Revision)).Result).StatusCode);
        var round = await Open(session.Id, mood);
        var own = Session(await As(pupil).Answer(pageId, session.Id, round.Id, new(0, null), default)).Round!;
        Assert.True(own.HasAnswered);
        Assert.Equal(0, own.YourChoice);
        Assert.Null(own.Counts);
        Assert.Null(Session(await As(peer).Get(pageId, default)).Round!.YourChoice);
        var reconnected = Session(await As(pupil).Get(pageId, default)).Round!;
        Assert.Equal(0, reconnected.YourChoice);

        await As(peer).Answer(pageId, session.Id, round.Id, new(2, null), default);
        await As(pupil).Answer(pageId, session.Id, round.Id, new(1, null), default);
        Assert.Equal(2, Session(await As(teacher).Get(pageId, default)).Round!.TotalAnswers);
        await Control(session.Id, round.Id, "show");
        var publishedCounts = Session(await As(peer).Get(pageId, default)).Round!.Counts;
        Assert.Equal([0, 1, 1, 0, 0], Assert.IsType<int[]>(publishedCounts));
        await Control(session.Id, round.Id, "hide");
        Assert.Null(Session(await As(peer).Get(pageId, default)).Round!.Counts);
        await Control(session.Id, round.Id, "close");
        Assert.IsType<ConflictObjectResult>((await As(pupil).Answer(pageId, session.Id, round.Id, new(2, null), default)).Result);
        Assert.Equal(0, (await Open(session.Id, scale)).TotalAnswers);
        Assert.Empty(await db.PollRounds.AsNoTracking().ToArrayAsync());

        Assert.Null(Ok(await As(teacher).Stop(pageId, session.Id, default)).Session);
        Assert.IsType<NotFoundResult>((await As(pupil).History(pageId, default)).Result);
        var history = Assert.Single(Ok(await As(teacher).History(pageId, default)));
        Assert.Equal(2, history.ParticipantCount);
        Assert.Equal([0, 1, 1, 0, 0], Assert.IsType<int[]>(history.Rounds.First(r => r.BlockId == "mood").Counts));
    }

    [Theory]
    [InlineData("brainstorm")]
    [InlineData("qa")]
    public async Task OnlyApprovedIdeasAreVisibleAndVoteIsUnique(string kind)
    {
        var module = await Save(kind, kind);
        var session = await Start();
        var round = await Open(session.Id, module);
        const string text = "Forslag\nmed en ny linje";
        var pending = Session(await As(pupil).Answer(pageId, session.Id, round.Id, new(null, text), default)).Round!;
        Assert.True(pending.HasAnswered);
        Assert.Null(pending.YourText);
        Assert.Empty(pending.Entries);
        var teacherEntry = Assert.Single(Session(await As(teacher).Get(pageId, default)).Round!.Entries);
        Assert.Equal(text, teacherEntry.Text);
        Assert.False(teacherEntry.Moderated);
        Assert.IsType<NotFoundResult>((await As(peer).Vote(pageId, session.Id, round.Id, teacherEntry.Id, new(true), default)).Result);
        Assert.IsType<NotFoundResult>((await As(peer).Moderate(pageId, session.Id, round.Id, teacherEntry.Id, new(true), default)).Result);
        await As(teacher).Moderate(pageId, session.Id, round.Id, teacherEntry.Id, new(true), default);
        Assert.Equal(text, Assert.Single(Session(await As(peer).Get(pageId, default)).Round!.Entries).Text);
        await As(peer).Vote(pageId, session.Id, round.Id, teacherEntry.Id, new(true), default);
        var vote = Assert.Single(Session(await As(peer).Vote(pageId, session.Id, round.Id, teacherEntry.Id, new(true), default)).Round!.Entries);
        Assert.True(vote.YourVote);
        Assert.Equal(1, vote.Votes);
        await As(teacher).Moderate(pageId, session.Id, round.Id, teacherEntry.Id, new(false), default);
        Assert.Empty(Session(await As(peer).Get(pageId, default)).Round!.Entries);
        await Control(session.Id, round.Id, "close");
        await As(teacher).Moderate(pageId, session.Id, round.Id, teacherEntry.Id, new(true), default);
        Assert.IsType<NotFoundResult>((await As(peer).Vote(pageId, session.Id, round.Id, teacherEntry.Id, new(false), default)).Result);
    }

    [Fact]
    public async Task OrdskyShowsIncomingWordsImmediatelyWithoutDoubleCountingOccurrences()
    {
        var module = await Save("ordsky", "word-cloud");
        var session = await Start();
        var round = await Open(session.Id, module);
        Assert.True(round.ShowResults);
        var answered = Session(await As(pupil).Answer(pageId, session.Id, round.Id, new(null, " API "), default)).Round!;
        Assert.Equal("API", answered.YourText);
        Assert.True(Assert.Single(answered.Entries).Approved);
        Assert.Equal(1, Assert.Single(Session(await As(peer).Get(pageId, default)).Round!.Entries).Votes);
        await As(peer).Answer(pageId, session.Id, round.Id, new(null, "api"), default);
        var presenter = Session(await As(teacher).Get(pageId, default)).Round!;
        Assert.All(presenter.Entries, entry => Assert.True(entry.Approved));
        Assert.Equal(2, presenter.Entries.Sum(item => item.Votes));
        var studentWord = Assert.Single(Session(await As(peer).Get(pageId, default)).Round!.Entries);
        Assert.Equal("api", studentWord.Text, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, studentWord.Votes);

        await Control(session.Id, round.Id, "hide");
        var hidden = Session(await As(peer).Get(pageId, default)).Round!;
        Assert.False(hidden.ShowResults);
        Assert.Empty(hidden.Entries);
        Assert.Null(hidden.TotalAnswers);
        await Control(session.Id, round.Id, "show");
        Assert.Equal(2, Assert.Single(Session(await As(peer).Get(pageId, default)).Round!.Entries).Votes);
    }

    [Fact]
    public async Task OrdskyShowsLegacyPendingWordsWhileKeepingExplicitlyHiddenWordsHidden()
    {
        var module = await Save("ordsky", "word-cloud");
        var session = await Start();
        var round = await Open(session.Id, module);
        var legacy = new LiveCourseEntry
        {
            Id = Guid.NewGuid(), RoundId = round.Id, UserSub = pupil, Text = "API", Approved = null,
            UpdatedAtUtc = clock.Now.UtcDateTime
        };
        var hidden = new LiveCourseEntry
        {
            Id = Guid.NewGuid(), RoundId = round.Id, UserSub = peer, Text = "api", Approved = false,
            UpdatedAtUtc = clock.Now.UtcDateTime
        };
        db.LiveCourseEntries.AddRange(legacy, hidden);
        await db.SaveChangesAsync();

        var visible = Session(await As(peer).Get(pageId, default)).Round!;
        Assert.Equal(1, visible.TotalAnswers);
        var word = Assert.Single(visible.Entries);
        Assert.Equal(legacy.Id, word.Id);
        Assert.True(word.Approved);
        Assert.Equal(1, word.Votes);
        Assert.Null(visible.YourText);
        Assert.Equal("API", Session(await As(pupil).Get(pageId, default)).Round!.YourText);
        var presenter = Session(await As(teacher).Get(pageId, default)).Round!;
        Assert.Equal(1, presenter.Entries.Sum(entry => entry.Votes));
        Assert.True(Assert.Single(presenter.Entries, entry => entry.Id == legacy.Id).Approved);
        Assert.False(Assert.Single(presenter.Entries, entry => entry.Id == hidden.Id).Approved);

        await As(teacher).Moderate(pageId, session.Id, round.Id, legacy.Id, new(false), default);
        Assert.Empty(Session(await As(peer).Get(pageId, default)).Round!.Entries);
        await As(pupil).Answer(pageId, session.Id, round.Id, new(null, "API"), default);
        Assert.Empty(Session(await As(peer).Get(pageId, default)).Round!.Entries);
    }

    [Fact]
    public async Task TextPollStillRequiresApprovalWhenResultsAreShown()
    {
        var module = await Save("text-poll", "poll", pollType: "text");
        var session = await Start();
        var round = await Open(session.Id, module);
        Assert.False(round.ShowResults);
        var answered = Session(await As(pupil).Answer(pageId, session.Id, round.Id, new(null, "Mit svar"), default)).Round!;
        Assert.Null(answered.YourText);
        await Control(session.Id, round.Id, "show");
        Assert.Empty(Session(await As(peer).Get(pageId, default)).Round!.Entries);
        var pending = Assert.Single(Session(await As(teacher).Get(pageId, default)).Round!.Entries);
        Assert.False(pending.Approved);
        Assert.False(pending.Moderated);
        await As(teacher).Moderate(pageId, session.Id, round.Id, pending.Id, new(true), default);
        Assert.Equal("Mit svar", Assert.Single(Session(await As(peer).Get(pageId, default)).Round!.Entries).Text);
    }

    [Fact]
    public async Task QuizAnswerKeyIsPrivateUntilRevealAndClockRejectsLateAnswers()
    {
        var module = await Save("quiz", "quiz", options: ["TLS", "SQL"], correctChoice: 1, duration: 10);
        var session = await Start();
        var round = await Open(session.Id, module);
        Assert.IsType<NotFoundResult>((await As(pupil).GetModule(pageId, module.Id, default)).Result);
        Assert.Null(Session(await As(pupil).Get(pageId, default)).Round!.CorrectChoice);
        Assert.True(Session(await As(pupil).Answer(pageId, session.Id, round.Id, new(1, null), default)).Round!.HasAnswered);
        Assert.IsType<ConflictObjectResult>((await As(pupil).Answer(pageId, session.Id, round.Id, new(0, null), default)).Result);
        clock.Now += TimeSpan.FromSeconds(11);
        Assert.False(Session(await As(peer).Get(pageId, default, session.Id, session.Revision + 2)).Round!.Open);
        Assert.IsType<ConflictObjectResult>((await As(peer).Answer(pageId, session.Id, round.Id, new(1, null), default)).Result);
        Assert.Equal(1, (await Control(session.Id, round.Id, "show")).CorrectChoice);
        var visible = Session(await As(teacher).Ranking(pageId, session.Id, new(true), default));
        var rank = Assert.Single(visible.Ranking);
        Assert.Equal("Deltager 1", rank.Label);
        Assert.Equal(1, rank.Score);
        Assert.Equal(1, Session(await As(peer).Get(pageId, default)).Round!.CorrectChoice);
    }

    [Fact]
    public async Task ReactionsExpireAndOutsidersCannotSendThem()
    {
        var session = await Start();
        Assert.IsType<NotFoundResult>((await As(outsider).React(pageId, session.Id, new("👍"), default)).Result);
        Assert.Single(Session(await As(pupil).React(pageId, session.Id, new("👍"), default)).Reactions);
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<ObjectResult>(
            (await As(pupil).React(pageId, session.Id, new("💡"), default)).Result).StatusCode);
        Assert.Equal("👍", Assert.Single(Session(await As(peer).Get(pageId, default)).Reactions).Emoji);
        clock.Now += TimeSpan.FromSeconds(13);
        Assert.Empty(Session(await As(peer).Get(pageId, default)).Reactions);
        await As(teacher).Stop(pageId, session.Id, default);
        Assert.Empty(await db.LiveCourseReactions.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task PrivacyExportIncludesOnlyTheCurrentParticipantsLiveActivity()
    {
        var module = await Save("ideas", "brainstorm");
        var session = await Start();
        var round = await Open(session.Id, module);
        Ok(await As(pupil).Answer(pageId, session.Id, round.Id, new(null, "Mit forslag"), default));
        Ok(await As(peer).Answer(pageId, session.Id, round.Id, new(null, "En andens forslag"), default));
        var entries = Session(await As(teacher).Get(pageId, default)).Round!.Entries;
        var pupilIdea = Assert.Single(entries, e => e.Text == "Mit forslag");
        var peerIdea = Assert.Single(entries, e => e.Text == "En andens forslag");
        foreach (var idea in new[] { pupilIdea, peerIdea })
            Ok(await As(teacher).Moderate(pageId, session.Id, round.Id, idea.Id, new(true), default));
        Ok(await As(pupil).Vote(pageId, session.Id, round.Id, peerIdea.Id, new(true), default));
        Ok(await As(peer).Vote(pageId, session.Id, round.Id, pupilIdea.Id, new(true), default));
        Ok(await As(pupil).React(pageId, session.Id, new("👍"), default));
        Ok(await As(peer).React(pageId, session.Id, new("💡"), default));

        var privacy = new PrivacyController(db) { ControllerContext = As(pupil).ControllerContext };
        using var export = JsonDocument.Parse(Assert.IsType<FileContentResult>(await privacy.Export(default)).FileContents);
        var records = export.RootElement.GetProperty("PlatformRecords");
        var answer = Assert.Single(records.GetProperty("liveCourseAnswers").EnumerateArray());
        Assert.Equal("Mit forslag", answer.GetProperty("Text").GetString());
        var vote = Assert.Single(records.GetProperty("liveCourseVotes").EnumerateArray());
        Assert.Equal(peerIdea.Id, vote.GetProperty("EntryId").GetGuid());
        var reaction = Assert.Single(records.GetProperty("liveCourseReactions").EnumerateArray());
        Assert.Equal("👍", reaction.GetProperty("Emoji").GetString());
    }

    private NotionPage Page(Guid id, Guid? parent) => new()
    {
        Id = id, HoldId = holdId, ParentPageId = parent, Title = "API i praksis", CreatedBySub = teacher,
        CreatedAtUtc = clock.Now, UpdatedAtUtc = clock.Now, PublishedAtUtc = clock.Now,
        PublishedRevision = Revision, PublicationVersion = 1, PublishedTitle = "API i praksis", PublishedState = [1, 2, 3]
    };

    private async Task<Guid> AddCourse(Guid resource, bool published)
    {
        var course = new TeachingCourse { Id = Guid.NewGuid(), HoldId = holdId, Title = "Web", Published = published };
        course.Resources.Add(new() { CourseId = course.Id, Kind = "page", ResourceId = resource });
        db.TeachingCourses.Add(course);
        await db.SaveChangesAsync();
        return course.Id;
    }

    private LiveCourseController As(Guid sub, bool teacherRole = false)
    {
        db.ChangeTracker.Clear();
        var claims = new List<Claim> { new("sub", sub.ToString()) };
        if (sub == teacher || teacherRole) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(db, new CourseAccess(db), new NotionAiProjectionClient(projection, config, new NotebookServiceKey(config)), clock)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } }
        };
    }

    private async Task<LiveCourseModuleView> Save(string block, string kind, string[]? options = null, string? pollType = null,
        int? correctChoice = null, int? duration = null, int slideIndex = 0, bool bind = true)
    {
        var module = Ok(await As(teacher).SaveModule(pageId,
            new(block, kind, "Hvad tænker du?", options, correctChoice, duration, pollType), default));
        if (bind) projection.Bindings.Add(new(block, module.Id, slideIndex));
        return module;
    }

    private Task<LiveCourseSessionView> Start() => Read(As(teacher).Start(pageId, new(Revision), default));

    private async Task<LiveCourseRoundView> Open(Guid session, LiveCourseModuleView module) =>
        (await Read(As(teacher).OpenRound(pageId, session, new(module.Id, module.BlockId), default))).Round!;

    private async Task<LiveCourseRoundView> Control(Guid session, Guid round, string action) =>
        (await Read(As(teacher).ControlRound(pageId, session, round, new(action), default))).Round!;

    private static async Task<LiveCourseSessionView> Read(Task<ActionResult<LiveCourseView>> value) => Session(await value);
    private static LiveCourseSessionView Session(ActionResult<LiveCourseView> result) => Assert.IsType<LiveCourseSessionView>(Ok(result).Session);
    private static T Ok<T>(ActionResult<T> result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    public async Task DisposeAsync()
    {
        projection.Dispose();
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ProjectionHandler : HttpMessageHandler, IHttpClientFactory
    {
        public List<LiveCourseBinding> Bindings { get; } = [];
        public int Calls { get; private set; }
        public string? PublishedBytes { get; private set; }
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal("/internal/notion/project", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("live-presentation", body.RootElement.GetProperty("mode").GetString());
            PublishedBytes = body.RootElement.GetProperty("stateBase64").GetString();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new LiveCourseProjection(2, Bindings.ToArray())) };
        }
    }
}
