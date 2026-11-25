using System.Text.Json;
using Mercantec.Tech.Api.SocialGames;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class WerewolfNarrationTests
{
    [Fact]
    public void Previous_snapshot_translates_the_night_secrets_and_reveal_without_translating_player_names()
    {
        var wolf = new WerewolfSeat(Guid.NewGuid(), "Anna og Tyv", "wolf");
        var lover = new WerewolfSeat(Guid.NewGuid(), "Bo, Heks", "guard");
        var seer = new WerewolfSeat(Guid.NewGuid(), "Heks er Jæger", "seer", false);
        var oldJson = JsonSerializer.Serialize(new
        {
            Seats = new[] { wolf, lover, seer }, Step = "finished", Round = 2,
            Lovers = new[] { wolf.Id, lover.Id },
            PublicMessage = $"Det er morgen efter nat 2. {seer.Name} (Seer) er ude.",
            Result = $"De forelskede, {wolf.Name} og {lover.Name}, vinder sammen!",
            Secrets = new Dictionary<Guid, string[]> { [seer.Id] = [$"Nat 2: {lover.Name} er Beskytter."],
                [wolf.Id] = [$"Du er forelsket i {lover.Name}."] }
        });
        var game = JsonSerializer.Deserialize<WerewolfGame>(oldJson)!;
        Assert.Null(game.AnnouncedDeaths);
        var en = game.Project(wolf.Id, false).English!;
        Assert.Equal("Dawn breaks after night 2. Heks er Jæger (Seer) is out. Role cards: Anna og Tyv (Werewolf), Bo, Heks (Protector), Heks er Jæger (Seer).", en.PublicMessage);
        Assert.Equal("The lovers, Anna og Tyv and Bo, Heks, win together!", en.Result);
        Assert.Equal("You are in love with Bo, Heks.", Assert.Single(en.PrivateMessages));
        Assert.Equal("Night 2: Bo, Heks has the role Protector.", Assert.Single(game.Project(seer.Id, false).English!.PrivateMessages));
        Assert.Equal("Du er forelsket i Bo, Heks.", Assert.Single(game.Project(wolf.Id, false).PrivateMessages));
    }

    [Fact]
    public void Wolves_see_the_named_vote_only_in_their_own_projection()
    {
        var game = new WerewolfGame
        {
            Seats = [new(Guid.NewGuid(), "Ulvene har valgt", "wolf"), new(Guid.NewGuid(), "Anna foreslår Bo", "wolf"),
                new(Guid.NewGuid(), "Heks", "villager")],
            Step = "wolves", WolvesHaveMet = true
        };
        var (viewer, voter, target) = (game.Seats[0], game.Seats[1], game.Seats[2]);
        game.Votes[voter.Id] = target.Id;
        var en = game.Project(viewer.Id, false).English!;
        Assert.Contains("Anna foreslår Bo suggests Heks.", en.PrivateMessages);
        Assert.Contains("Your fellow werewolves: Anna foreslår Bo.", en.PrivateMessages);
        Assert.Empty(game.Project(target.Id, true).English!.PrivateMessages);
    }

    [Fact]
    public void Saved_death_ids_disambiguate_a_nickname_that_looks_like_an_elimination()
    {
        var game = new WerewolfGame
        {
            Step = "day",
            Seats = [new(Guid.NewGuid(), "A", "thief", false), new(Guid.NewGuid(), "B", "villager", false),
                new(Guid.NewGuid(), "A (Tyv) er ude. B", "villager", false), new(Guid.NewGuid(), "Sofie", "wolf")],
            PublicMessage = "Landsbyen har stemt. A (Tyv) er ude. B (Landsbyboer) er ude."
        };
        // An older snapshot can coincide with two possible eliminations, so use the actual public roster.
        Assert.Contains("Players out so far:", game.Project(game.Seats[3].Id, false).English!.PublicMessage);
        game.AnnouncedDeaths = [game.Seats[2].Id];
        Assert.Equal("The village has voted. A (Tyv) er ude. B (Villager) is out.", game.Project(game.Seats[3].Id, false).English!.PublicMessage);
    }
}
