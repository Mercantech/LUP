using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Avatars;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Pages.Teacher;
using Mercantec.Tech.Web.Students;
using Microsoft.Extensions.DependencyInjection;
using StudentsPage = Mercantec.Tech.Web.Pages.Teacher.Students;

namespace Mercantec.Tech.Web.Tests;

public sealed class StudentDirectoryUiTests
{
    private static readonly Guid FirstHold = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid SecondHold = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Students_and_teacher_preview_do_not_fetch_the_directory(bool teacher, bool preview)
    {
        using var api = new DirectoryApi([Student("Anne", "anne")]);
        await using var context = await CreateContext(api, teacher, preview);
        var page = context.Render<StudentsPage>();

        Assert.Contains("Denne side er for undervisere", page.Markup);
        Assert.Empty(page.FindAll(".directory-table"));
        Assert.Equal(0, api.Loads);
    }

    [Fact]
    public async Task Teacher_searches_across_holds_school_periods_and_workplaces_and_combines_filters()
    {
        var anne = Student("Anne Andersen", "anne", "school", "H2", "Nordværk", FirstHold, "Programmering", "Objektorienteret kode");
        var bo = Student("Bo Bak", "bo", "workplace", "GF2", "Syddata", SecondHold, "Datateknik");
        var unknown = Student("Carla C", "carla");
        // The same person is still one row when multiple sources provide their memberships.
        var duplicate = anne with { Key = "anne-source-2", Holds = [new(SecondHold, "Datateknik", null)] };
        using var api = new DirectoryApi([anne, bo, unknown, duplicate]);
        await using var context = await CreateContext(api);
        var page = context.Render<StudentsPage>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("tbody tr").Count));

        var search = page.Find("input[type=search]");
        search.Input("nordVÆRK");
        Assert.Equal("anne", Assert.Single(page.FindAll("tbody tr")).GetAttribute("data-student-key"));
        search.Input("objektorienteret");
        Assert.Single(page.FindAll("tbody tr"));
        search.Input("gf2");
        Assert.Equal("bo", Assert.Single(page.FindAll("tbody tr")).GetAttribute("data-student-key"));
        search.Input("");

        page.FindAll(".directory-filters select")[1].Change(SecondHold.ToString());
        Assert.Equal(2, page.FindAll("tbody tr").Count);
        page.FindAll(".directory-filters select")[0].Change("workplace");
        Assert.Equal("bo", Assert.Single(page.FindAll("tbody tr")).GetAttribute("data-student-key"));
        page.FindAll(".directory-filters select")[2].Change("H2");
        Assert.Contains("Ingen elever matcher", page.Markup);
        page.Find(".directory-clear").Click();
        page.FindAll(".directory-filters select")[0].Change("unknown");
        Assert.Equal("carla", Assert.Single(page.FindAll("tbody tr")).GetAttribute("data-student-key"));
    }

    [Fact]
    public async Task Paging_bounds_photo_components_and_search_returns_to_the_first_page()
    {
        using var api = new DirectoryApi(Enumerable.Range(1, 85).Select(index => Student($"Elev {index:000}", $"elev-{index}")).ToList());
        await using var context = await CreateContext(api);
        var page = context.Render<StudentsPage>();
        page.WaitForAssertion(() => Assert.Equal(40, page.FindAll("tbody tr").Count));
        Assert.Equal(40, page.FindComponents<Bunit.TestDoubles.Stub<StudentPhoto>>().Count);
        page.FindAll(".directory-pagination button")[1].Click();
        Assert.Contains("Side 2 af 3", page.Markup);
        Assert.Equal("elev-41", page.Find("tbody tr").GetAttribute("data-student-key"));
        page.Find("input[type=search]").Input("Elev 085");
        Assert.Equal("elev-85", Assert.Single(page.FindAll("tbody tr")).GetAttribute("data-student-key"));
        Assert.Contains("Side 1 af 1", page.Markup);
    }

    [Fact]
    public async Task Failed_save_keeps_the_draft_and_retry_patches_only_changed_fields()
    {
        var student = Student("Anne Andersen", "lectio:42", "school", "H2", "Nordværk") with { LastPassedSchoolPeriod = "H1" };
        using var api = new DirectoryApi([student]) { FailNextSave = true };
        await using var context = await CreateContext(api);
        var page = context.Render<StudentsPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        page.Find(".directory-row-actions button").Click();
        page.Find("#student-edit-placement").Change("workplace");
        page.Find("#student-edit-workplace").Change("Ny virksomhed");
        page.Find("#student-edit-passed").Change("");
        page.Find(".directory-editor form").Submit();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".directory-editor [role=alert]")));
        Assert.Equal("Ny virksomhed", page.Find("#student-edit-workplace").GetAttribute("value"));
        Assert.Equal("", page.Find("#student-edit-passed").GetAttribute("value"));
        Assert.Equal("workplace", api.Saves[0].Placement);
        Assert.Null(api.Saves[0].CurrentSchoolPeriod);
        Assert.Equal("Ny virksomhed", api.Saves[0].WorkplaceName);
        Assert.Equal("", api.Saves[0].LastPassedSchoolPeriod);
        Assert.Equal("/api/students/lectio:42/details", api.LastSavePath);

        page.Find(".directory-editor form").Submit();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".directory-editor")));
        Assert.Equal(2, api.Saves.Count);
        Assert.Contains("Ny virksomhed", page.Find("tbody tr").TextContent);
        Assert.Contains("Ikke registreret", page.Find("tbody tr").TextContent);
    }

    [Fact]
    public async Task Reset_discards_teacher_corrections_and_account_preview_clears_open_editing()
    {
        var original = Student("Anne", "anne", "school", "H2", "Nordværk");
        using var api = new DirectoryApi([original with { Placement = "workplace", HasTeacherOverrides = true }]) { ResetStudent = original };
        await using var context = await CreateContext(api);
        var page = context.Render<StudentsPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        page.Find(".directory-row-actions button").Click();
        page.Find(".directory-reset").Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".directory-editor")));
        Assert.True(Assert.Single(api.Saves).ResetToLectio);
        Assert.Contains("På skole", page.Find("tbody tr").TextContent);

        page.Find(".directory-row-actions button").Click();
        page.Find("#student-edit-workplace").Change("Ikke gemt");
        context.ViewAs.Enter("preview", "Elev");
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".student-directory")));
        context.ViewAs.Exit();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.Empty(page.FindAll(".directory-editor"));
        Assert.DoesNotContain("Ikke gemt", page.Markup);
        Assert.Equal(2, api.Loads);
    }

    [Fact]
    public async Task Photo_opens_the_students_explicit_avatar_and_unregistered_student_has_no_avatar_fallback()
    {
        var avatar = AvatarCatalog.All.First(definition => definition.Id != AvatarCatalog.StarterId);
        using var api = new DirectoryApi([
            Student("Anne", "anne") with { UserSub = Guid.NewGuid(), SelectedAvatarId = avatar.Id },
            Student("Bo", "bo")
        ]);
        await using var context = await CreateContext(api, stubPhotos: false);
        var page = context.Render<StudentsPage>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        Assert.Equal("Anne", page.Find(".student-photo").GetAttribute("aria-label"));
        Assert.Equal("A", page.Find(".student-photo").TextContent.Trim());
        page.Find("button[aria-label='Se avatar for Anne']").Click();
        Assert.Equal(avatar.Id, page.Find(".directory-avatar-dialog [data-avatar-icon]").GetAttribute("data-avatar-icon"));
        page.Find("button[aria-label='Se avatar for Bo']").Click();
        Assert.Empty(page.FindAll(".directory-avatar-dialog [data-avatar-icon]"));
        Assert.Contains("Eleven har endnu ikke en tilknyttet LUP-bruger", page.Find(".directory-avatar-dialog").TextContent);
    }

    private static async Task<LearningUiTestContext> CreateContext(DirectoryApi api, bool teacher = true, bool preview = false, bool stubPhotos = true)
    {
        var context = await LearningUiTestContext.CreateAsync(api, teacher, preview, services =>
        {
            services.AddSingleton<StudentDirectoryClient>();
            services.AddSingleton<ProfilePhotoClient>();
            services.AddSingleton<IAvatarClient, AvatarClient>();
            services.AddSingleton<DemoWorld>();
        });
        if (stubPhotos) context.ComponentFactories.AddStub<StudentPhoto>();
        return context;
    }

    private static StudentDirectoryStudent Student(string name, string key, string placement = "unknown", string? school = null,
        string? workplace = null, Guid? hold = null, string holdName = "Hold", string? course = null) =>
        new(key, key, null, name, null, hold is Guid id ? [new(id, holdName, course)] : [], placement, school, workplace,
            null, false, null, false, null);

    private sealed class DirectoryApi(List<StudentDirectoryStudent> students) : HttpMessageHandler
    {
        public int Loads { get; private set; }
        public bool FailNextSave { get; set; }
        public StudentDirectoryStudent? ResetStudent { get; init; }
        public string? LastSavePath { get; private set; }
        public List<UpdateStudentDirectoryDetails> Saves { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Get)
            {
                if (request.RequestUri!.AbsolutePath.StartsWith("/api/lectio/students/", StringComparison.Ordinal))
                    return new(HttpStatusCode.NotFound);
                Assert.Equal("/api/students", request.RequestUri?.AbsolutePath);
                Loads++;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new StudentDirectoryResponse(students, true, null)) };
            }
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal(LearningUiTestContext.AccountId.ToString(), Assert.Single(request.Headers.GetValues(ProfileNameRules.SubjectHeader)));
            LastSavePath = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
            var details = (await request.Content!.ReadFromJsonAsync<UpdateStudentDirectoryDetails>(ct))!;
            Saves.Add(details);
            if (FailNextSave)
            {
                FailNextSave = false;
                return new(HttpStatusCode.ServiceUnavailable);
            }
            var current = students[0];
            var updated = details.ResetToLectio ? ResetStudent! : current with
            {
                Placement = details.Placement ?? current.Placement,
                CurrentSchoolPeriod = details.CurrentSchoolPeriod ?? current.CurrentSchoolPeriod,
                WorkplaceName = details.WorkplaceName ?? current.WorkplaceName,
                LastPassedSchoolPeriod = details.LastPassedSchoolPeriod ?? current.LastPassedSchoolPeriod,
                HasTeacherOverrides = true
            };
            students[0] = updated;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(updated) };
        }
    }
}
