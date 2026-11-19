using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Api.Services;
using Mercantec.Tech.Api.Students;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mercantec.Tech.Api.Tests;

public sealed class StudentDirectoryTests
{
    [Fact]
    public async Task Global_directory_combines_all_holds_and_lectio_once_and_excludes_known_teachers()
    {
        await using var fixture = await Fixture.Create();
        var studentSub = Guid.NewGuid();
        fixture.Member("111", studentSub, "Importnavn", "H1");
        fixture.Member("111", studentSub, "Importnavn", "Web");
        fixture.Member(null, fixture.Teacher, "Underviser", "Lærerhold");
        fixture.Db.NotebookProfiles.Add(new() { Sub = fixture.Teacher, DisplayName = "Underviser", IsTeacher = true });
        fixture.Db.ProfileNames.Add(new() { UserSub = studentSub, DisplayName = "Elevens navn" });
        fixture.Db.AvatarProfiles.Add(new() { UserSub = studentSub, SelectedAvatarId = "raymond" });
        fixture.Db.ProfilePhotos.Add(new() { UserSub = studentSub, StorageKey = "private-photo", ContentType = "image/png", SizeBytes = 12 });
        await fixture.Db.SaveChangesAsync();
        fixture.Handler.Students = """{"students":[{"lectioId":"111","name":"Lectio-navn","className":"H1"},{"lectioId":"222","name":"Endnu ikke på LUP"}]}""";

        var result = await fixture.Service.ListAsync("fixture-token", requestingTeacher: fixture.Teacher);

        Assert.True(result.LectioAvailable);
        Assert.Equal(2, result.Students.Count);
        var student = Assert.Single(result.Students, s => s.UserSub == studentSub);
        Assert.Equal("lectio:111", student.Key);
        Assert.Equal("Elevens navn", student.DisplayName);
        Assert.Equal(2, student.Holds.Count);
        Assert.Equal("raymond", student.SelectedAvatarId);
        Assert.True(student.HasCustomPhoto);
        Assert.Contains(result.Students, s => s.Key == "lectio:222" && s.UserSub is null);
        Assert.Equal(new[] { "/api/programmeringselever", "/api/auth-links/resolve" }, fixture.Handler.Paths);
    }

    [Fact]
    public async Task Students_cannot_read_or_change_the_staff_directory()
    {
        await using var fixture = await Fixture.Create();
        var api = fixture.Controller(teacher: false);

        Assert.IsType<ForbidResult>((await api.List(default)).Result);
        Assert.IsType<ForbidResult>((await api.Update("lectio:111", new(null, null, null, "H1"), default)).Result);
        Assert.Empty(fixture.Handler.Paths);
        Assert.Empty(await fixture.Db.StudentDirectoryDetails.ToArrayAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unavailable_or_malformed_collector_keeps_local_students_and_corrections(bool transportFailure)
    {
        await using var fixture = await Fixture.Create();
        var student = Guid.NewGuid();
        fixture.Member(null, student, "Lokal elev", "Lokalt hold");
        fixture.Db.StudentDirectoryDetails.Add(new() { Key = "user:" + student.ToString("D"), UserSub = student, Placement = "workplace", WorkplaceName = "Værksted", LastPassedSchoolPeriod = "GF2" });
        await fixture.Db.SaveChangesAsync();
        fixture.Handler.Fail = transportFailure;
        fixture.Handler.Students = """{"error":"upstream_failed","students":null}""";

        var response = await fixture.Service.ListAsync("fixture-token");

        Assert.False(response.LectioAvailable);
        Assert.NotNull(response.Warning);
        var item = Assert.Single(response.Students);
        Assert.Equal("Lokal elev", item.DisplayName);
        Assert.Equal("workplace", item.Placement);
        Assert.Equal("GF2", item.LastPassedSchoolPeriod);
        Assert.True(item.HasTeacherOverrides);
    }

    [Fact]
    public async Task School_dates_are_inclusive_in_Copenhagen_and_ended_periods_do_not_prove_pass_or_workplace()
    {
        await using var fixture = await Fixture.Create();
        // Denmark is already on 19 November while the server's UTC date is still the 18th.
        fixture.Clock.Now = DateTimeOffset.Parse("2026-11-18T23:30:00Z");
        fixture.Handler.Students = """
            {"students":[
              {"lectioId":"current","name":"På skole","h6":{"name":"Programmering","level":"H2","startDate":"2026-11-19","endDate":"2026-11-19"}},
              {"lectioId":"ended","name":"Afsluttet","h6":{"level":"H1","startDate":"2026-10-01","endDate":"2026-11-18","passed":false},"workplace":{"name":"Firma"}},
              {"lectioId":"work","name":"På læreplads","workplace":{"name":"Firma","isCurrent":true}},
              {"lectioId":"passed","name":"Bestået","schoolPeriods":[{"name":"Web","level":"H1","endDate":"2026-11-01","passed":true},{"level":"H2","endDate":"2026-11-18","passed":false}]}
            ]}
            """;

        var response = await fixture.Service.ListAsync("fixture-token");

        var current = response.Students.Single(s => s.LectioId == "current");
        Assert.Equal("school", current.Placement);
        Assert.Equal("H2", current.CurrentSchoolPeriod);
        var ended = response.Students.Single(s => s.LectioId == "ended");
        Assert.Equal("unknown", ended.Placement);
        Assert.Null(ended.LastPassedSchoolPeriod);
        Assert.Equal("Firma", ended.WorkplaceName);
        Assert.Equal("workplace", response.Students.Single(s => s.LectioId == "work").Placement);
        Assert.Equal("H1", response.Students.Single(s => s.LectioId == "passed").LastPassedSchoolPeriod);
    }

    [Fact]
    public async Task Shadow_corrections_follow_the_student_when_memberships_are_claimed()
    {
        await using var fixture = await Fixture.Create();
        var student = Guid.NewGuid();
        fixture.Member("111", null, "Før konto", "H1");
        await fixture.Db.SaveChangesAsync();
        var before = Assert.Single((await fixture.Service.ListAsync("fixture-token")).Students);
        await fixture.Service.UpdateAsync(before, new("workplace", null, "Praktiksted", "GF2"), fixture.Teacher, default);

        await new MembershipClaimService(fixture.Db).ClaimByLectioIdAsync(student, "111");
        var after = Assert.Single((await fixture.Service.ListAsync("fixture-token")).Students);

        Assert.Equal(before.Key, after.Key);
        Assert.Equal(student, after.UserSub);
        Assert.Equal("Praktiksted", after.WorkplaceName);
        Assert.Equal("GF2", after.LastPassedSchoolPeriod);
        Assert.True(after.HasTeacherOverrides);
        Assert.Equal(student, (await fixture.Db.StudentDirectoryDetails.SingleAsync()).UserSub);
    }

    [Fact]
    public async Task User_key_corrections_survive_a_new_lectio_link_and_patch_preserves_previous_fields()
    {
        await using var fixture = await Fixture.Create();
        var student = Guid.NewGuid();
        var membership = fixture.Member(null, student, "Konto først", "H1");
        await fixture.Db.SaveChangesAsync();
        var before = Assert.Single((await fixture.Service.ListAsync("fixture-token")).Students);
        await fixture.Service.UpdateAsync(before, new("workplace", null, "Firma", "GF2"), fixture.Teacher, default);
        membership.LectioId = "111";
        await fixture.Db.SaveChangesAsync();
        var linked = Assert.Single((await fixture.Service.ListAsync("fixture-token")).Students);
        Assert.Equal("lectio:111", linked.Key);
        Assert.Equal("Firma", linked.WorkplaceName);

        await fixture.Service.UpdateAsync(linked, new(null, null, null, "H1"), fixture.Teacher, default);
        var updated = Assert.Single((await fixture.Service.ListAsync("fixture-token")).Students);

        Assert.Equal("Firma", updated.WorkplaceName);
        Assert.Equal("workplace", updated.Placement);
        Assert.Equal("H1", updated.LastPassedSchoolPeriod);
        Assert.Equal("lectio:111", (await fixture.Db.StudentDirectoryDetails.SingleAsync()).Key);
        await fixture.Service.UpdateAsync(updated, new(null, null, null, ""), fixture.Teacher, default);
        Assert.Null(Assert.Single((await fixture.Service.ListAsync("fixture-token")).Students).LastPassedSchoolPeriod);
    }

    [Fact]
    public async Task Forged_keys_and_invalid_values_are_rejected_and_reset_restores_source()
    {
        await using var fixture = await Fixture.Create();
        fixture.Member("111", null, "Elev", "H1");
        await fixture.Db.SaveChangesAsync();
        var api = fixture.Controller();
        Assert.IsType<NotFoundResult>((await api.Update("lectio:forged", new("school", "H9", null, "H8"), default)).Result);
        Assert.IsType<BadRequestObjectResult>((await api.Update("lectio:111", new(null, null, "Bad\nvalue", null), default)).Result);
        Assert.Empty(await fixture.Db.StudentDirectoryDetails.ToArrayAsync());
        var saved = Value(await api.Update("lectio:111", new("workplace", null, "Firma", "H1"), default));
        Assert.Equal("H1", saved.LastPassedSchoolPeriod);

        var reset = Value(await api.Update("lectio:111", new(null, null, null, null, ResetToLectio: true), default));

        Assert.False(reset.HasTeacherOverrides);
        Assert.Equal("unknown", reset.Placement);
        Assert.Null(reset.WorkplaceName);
        Assert.Null(reset.LastPassedSchoolPeriod);
        Assert.Empty(await fixture.Db.StudentDirectoryDetails.ToArrayAsync());
    }

    [Fact]
    public async Task External_account_link_does_not_reassign_a_claimed_membership()
    {
        await using var fixture = await Fixture.Create();
        var known = Guid.NewGuid();
        fixture.Member("111", known, "Kendt elev", "H1");
        await fixture.Db.SaveChangesAsync();
        fixture.Handler.Students = """{"students":[{"lectioId":"111","name":"Lectio"}]}""";
        fixture.Handler.Links["111"] = Guid.NewGuid();

        var student = Assert.Single((await fixture.Service.ListAsync("fixture-token")).Students);

        Assert.Equal(known, student.UserSub);
    }

    [Fact]
    public async Task A_correction_for_another_lectio_identity_is_not_reused_for_the_same_account()
    {
        await using var fixture = await Fixture.Create();
        var student = Guid.NewGuid();
        fixture.Member("new-person", student, "Ny identitet", "H1");
        fixture.Db.StudentDirectoryDetails.Add(new() { Key = "lectio:old-person", LectioId = "old-person", UserSub = student, LastPassedSchoolPeriod = "H5", WorkplaceName = "Tidligere firma" });
        await fixture.Db.SaveChangesAsync();

        var item = Assert.Single((await fixture.Service.ListAsync("fixture-token")).Students);

        Assert.False(item.HasTeacherOverrides);
        Assert.Null(item.LastPassedSchoolPeriod);
        Assert.Null(item.WorkplaceName);
    }

    [Fact]
    public async Task Writes_reject_a_request_started_by_another_teacher_account()
    {
        await using var fixture = await Fixture.Create();
        var api = fixture.Controller();
        api.Request.Headers[ProfileNameRules.SubjectHeader] = Guid.NewGuid().ToString();

        Assert.IsType<ConflictObjectResult>((await api.Update("lectio:111", new(null, null, null, "H1"), default)).Result);
        Assert.Empty(fixture.Handler.Paths);
    }

    private static StudentDirectoryStudent Value(ActionResult<StudentDirectoryStudent> result) =>
        Assert.IsType<StudentDirectoryStudent>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private sealed class Fixture(SqliteConnection connection, AppDbContext db) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = db;
        public Guid Teacher { get; } = Guid.NewGuid();
        public Collector Handler { get; } = new();
        public FixedClock Clock { get; } = new();
        private HttpClient? http;
        public StudentDirectoryService Service => new(Db, new LectioCollectorClient(http ??= new(Handler) { BaseAddress = new("https://collector.example/api/") }, NullLogger<LectioCollectorClient>.Instance), NullLogger<StudentDirectoryService>.Instance, Clock);

        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }

        public Membership Member(string? lectioId, Guid? subject, string name, string holdName)
        {
            var member = new Membership
            {
                Id = Guid.NewGuid(), LectioId = lectioId, UserSub = subject, DisplayName = name,
                Hold = new() { Id = Guid.NewGuid(), Name = holdName, Forloeb = new() { Id = Guid.NewGuid(), Name = holdName + " forløb", CreatedBySub = Teacher } }
            };
            Db.Memberships.Add(member);
            return member;
        }

        public StudentDirectoryController Controller(bool teacher = true)
        {
            var claims = new List<Claim> { new("sub", Teacher.ToString()) };
            if (teacher) claims.Add(new("role", "Teacher"));
            var context = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) };
            context.Request.Headers.Authorization = "Bearer fixture-token";
            context.Request.Headers[ProfileNameRules.SubjectHeader] = Teacher.ToString();
            return new(Service) { ControllerContext = new() { HttpContext = context } };
        }

        public async ValueTask DisposeAsync()
        {
            http?.Dispose();
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-11-19T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Collector : HttpMessageHandler
    {
        public string Students { get; set; } = """{"students":[]}""";
        public Dictionary<string, Guid> Links { get; } = [];
        public bool Fail { get; set; }
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("fixture-token", request.Headers.Authorization?.Parameter);
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            if (Fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            HttpContent content = path.EndsWith("programmeringselever", StringComparison.Ordinal)
                ? new StringContent(Students, System.Text.Encoding.UTF8, "application/json")
                : JsonContent.Create(new { links = Links.Select(link => new { lectioId = link.Key, authSub = link.Value }).ToArray() });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
