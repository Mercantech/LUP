using Mercantec.Tech.Api.Notebooks;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotebookCoverStyleTests
{
    [Theory]
    [InlineData("H2 – Docker Compose", "whale", "deploy-or-die")]
    [InlineData("Game-design II", "lupp", "game-design")]
    [InlineData("Beslutningstræer med scikit-learn", "chameleon", "machine-learning")]
    [InlineData("C# og ASP.NET Core", "bilbo", "valgfag-overblik")]
    [InlineData("SQL · INNER JOIN", "raymond", "valgfag-overblik")]
    [InlineData("IoT over MQTT", "bat", "valgfag-overblik")]
    [InlineData("Regressionstest med breakpoints", "gummianden", "valgfag-overblik")]
    public void Recognizes_specific_subjects_and_uses_existing_art(string title, string expectedCharacter, string expectedBanner)
    {
        var art = NotebookCoverStyle.Select("notion-page", title, [], []);

        Assert.Equal(expectedCharacter, art.CharacterSlug);
        var portrait = Assert.IsType<string>(art.CharacterAssetPath);
        Assert.StartsWith($"images/characters/{expectedCharacter}/reactions/", portrait, StringComparison.Ordinal);
        Assert.EndsWith(".webp", portrait, StringComparison.Ordinal);
        Assert.Equal($"images/valgfag/{expectedBanner}.webp", art.StyleAssetPath);
    }

    [Fact]
    public void Emoji_identity_wins_without_changing_course_art()
    {
        var art = NotebookCoverStyle.Select("notion-page", "SQL og tabeller", [],
            ["../../secret", "BAT", "raymond"], "Machine Learning");

        Assert.Equal("bat", art.CharacterSlug);
        Assert.Equal("images/characters/bat/reactions/wave.webp", art.CharacterAssetPath);
        Assert.Equal("images/valgfag/machine-learning.webp", art.StyleAssetPath);
        Assert.Contains("Næsebladet", art.PromptDirection, StringComparison.Ordinal);
        Assert.DoesNotContain("../../secret", art.PromptDirection, StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_authorized_course_before_incidental_excerpt_topic()
    {
        var art = NotebookCoverStyle.Select("collection", "Dag 2", ["Log ind på Git og hent materialet."], [], "Deploy or Die");

        Assert.Equal("whale", art.CharacterSlug);
        Assert.Equal("images/valgfag/deploy-or-die.webp", art.StyleAssetPath);
        Assert.Contains("notebook as a whole", art.PromptDirection, StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_excerpts_when_the_title_does_not_identify_the_subject()
    {
        var art = NotebookCoverStyle.Select("notebook-page", "Dag 2", ["En INNER JOIN forbinder rækker med en fælles nøgle."], []);

        Assert.Equal("raymond", art.CharacterSlug);
    }

    [Fact]
    public void Keeps_generic_metadata_neutral_and_does_not_match_substrings()
    {
        var art = NotebookCoverStyle.Select("notebook", "Min digitale logbog",
            ["Ugens modeller, test og projekter."], ["unknown"]);

        Assert.Null(art.CharacterSlug);
        Assert.Equal("images/characters/lupp/reactions/hello.webp", art.CharacterAssetPath);
        Assert.Contains("only for tactile clay", art.PromptDirection, StringComparison.Ordinal);
        Assert.Equal("images/valgfag/valgfag-overblik.webp", art.StyleAssetPath);
    }
}
