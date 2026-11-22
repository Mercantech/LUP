using Mercantec.Tech.Api.Notebooks;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotebookCoverCourseLabelTests
{
    [Fact]
    public void Prefers_the_course_period_over_project_and_deadline_dates()
    {
        var label = NotebookCoverCourseLabel.Create("Machine Learning 2027 :chameleon_test_data:", """
            Velkommen til ML 2027 - Online
            Undervisningen foregår online fra mandag 15. februar til fredag 5. marts 2027. Vælg en dato for at åbne dagens side.
            Fra onsdag 24. februar bruger du dag 8–15 på dit eget projekt. Fredag 5. marts afleverer du en videodemo.
            Uge 7
            """);

        Assert.NotNull(label);
        Assert.Equal("Machine Learning 2027", label.Title);
        Assert.Equal(["15. februar – 5. marts 2027"], label.Dates);
    }

    [Theory]
    [InlineData("26.–30. oktober 2026", "26.–30. oktober 2026")]
    [InlineData("26.10.2026-30.10.2026", "26.10.2026 – 30.10.2026")]
    [InlineData("2026-10-26 – 2026-10-30", "2026-10-26 – 2026-10-30")]
    [InlineData("26/10-30/10", "26/10 – 30/10")]
    [InlineData("26.10.–30.10.", "26.10. – 30.10.")]
    public void Keeps_the_dates_and_years_written_on_the_page(string text, string expected)
    {
        var label = NotebookCoverCourseLabel.Create("Webudvikling", $"Forløbsperiode: {text}");

        Assert.NotNull(label);
        Assert.Equal([expected], label.Dates);
    }

    [Fact]
    public void Reads_start_and_end_from_separate_fields_and_deduplicates()
    {
        var label = NotebookCoverCourseLabel.Create(" :lupp_wave:   Spiludvikling på øen ", """
            Startdato

            26/10
            Slutdato: 30. okt. 2026
            Slutdato: 30. okt. 2026
            Husk at bestille plads 10/09/2026.
            """);

        Assert.NotNull(label);
        Assert.Equal("Spiludvikling på øen", label.Title);
        Assert.Equal(["26/10", "30. okt. 2026"], label.Dates);
    }

    [Fact]
    public void Ignores_invalid_dates_and_dates_in_links_or_code()
    {
        var label = NotebookCoverCourseLabel.Create("Lær at læse 2027-data", """
            Datoer: 31. februar 2027, 2027-02-29 og 20/13/2027.
            https://lup.dk/2027-03-05
            Se eksempel: `test`: `2027-10-03`.
            const releaseDate = "2027-03-05";
            Version v1.2.2027 er aktive øvelse.
            Øvelse 1/4. Forløbet varer 10 dage.
            """);

        Assert.NotNull(label);
        Assert.Empty(label.Dates);
        Assert.Null(NotebookCoverCourseLabel.Create(":lupp_wave:   ", "Forløbsperiode: 26.–30. oktober 2026"));
    }

    [Fact]
    public void Uses_a_week_range_only_if_no_calendar_dates_were_found()
    {
        var weeks = NotebookCoverCourseLabel.Create("Spildesign", "Uger: 43, 44 & 45");
        var date = NotebookCoverCourseLabel.Create("Spildesign", "Uger: 43, 44 & 45\nWorkshop afholdes 24. oktober.");

        Assert.NotNull(weeks);
        Assert.Equal(["Uger: 43, 44 & 45"], weeks.Dates);
        Assert.NotNull(date);
        Assert.Equal(["24. oktober"], date.Dates);
    }

    [Fact]
    public void Bounds_how_many_dates_are_printed()
    {
        var label = NotebookCoverCourseLabel.Create("Netværksforløb", "Datoer: 1. maj, 2. maj, 3. maj, 4. maj.");

        Assert.NotNull(label);
        Assert.Equal(["1. maj", "2. maj", "3. maj"], label.Dates);
    }

    [Theory]
    [InlineData(":lupp_wave: Game Design 2027", "", "Game Design 2027")]
    [InlineData("Machine Learning – Confusion Matrix", "", null)]
    [InlineData("Deploy or Die", "", "Deploy or Die")]
    [InlineData("Forløb: Spildesign", "", "Spildesign")]
    [InlineData("Introduktion", "Forløbsnavn: Spildesign", "Spildesign")]
    [InlineData("Fra kode til drift", "Sådan bruger vi Git i vores forløb.", null)]
    public void Recognizes_only_explicit_standalone_courses(string title, string pageText, string? expected) =>
        Assert.Equal(expected, NotebookCoverCourseLabel.FindCourseTitle(title, pageText));
}
