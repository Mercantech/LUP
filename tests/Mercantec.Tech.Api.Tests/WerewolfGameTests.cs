using System.Text.Json;
using Mercantec.Tech.Api.SocialGames;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class WerewolfGameTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Deal_preserves_selected_cards_including_two_spares_for_the_thief()
    {
        var players = Enumerable.Range(1, 12).Select(i => new SocialPlayer(Guid.NewGuid(), $"Nr. {i}", false)).ToArray();
        var settings = new SocialGameSettings(SocialRoles.Specials, null, true);
        var game = WerewolfGame.Start(players, settings, Now);
        var deck = game.Seats.Select(p => p.Role).Concat(game.Spares).ToArray();
        Assert.Equal(2, game.Spares.Length);
        Assert.Equal(3, deck.Count(r => r == "wolf"));
        Assert.Equal(5, deck.Count(r => r == "villager"));
        Assert.All(SocialRoles.Specials, special => Assert.Single(deck, r => r == special));
        Assert.NotNull(WerewolfGame.StartProblem(5, new(SocialRoles.Specials, 1, true)));
    }

    [Fact]
    public void Seer_information_and_action_clocks_are_private_even_from_the_host()
    {
        var game = Scenario("seer", "seer", "wolf", "witch", "villager", "villager");
        var host = game.Seats[3].Id;
        var waiting = game.Project(host, true);
        Assert.Empty(waiting.Actions);
        Assert.Equal("night", waiting.Phase);
        Assert.Null(waiting.Deadline);
        Assert.Equal(0, waiting.PhaseId);
        Assert.Throws<WerewolfException>(() => game.Act(host, true, new(game.PhaseId, "inspect", [game.Seats[2].Id.ToString()]), Now));
        Choose(game, 0, "inspect", 2);
        var resumed = JsonSerializer.Deserialize<WerewolfGame>(JsonSerializer.Serialize(game))!;
        Assert.Contains("Nat 1: C er Heks.", resumed.Project(game.Seats[0].Id, false).PrivateMessages);
        Assert.Empty(resumed.Project(host, true).PrivateMessages);
        Assert.Equal(JsonSerializer.Serialize(waiting), JsonSerializer.Serialize(resumed.Project(host, true)));
        Assert.Equal("wolves", resumed.Step);
    }

    [Fact]
    public void Thief_can_become_cupid_and_the_chosen_pair_know_only_their_partner()
    {
        var game = Scenario("thief", "thief", "wolf", "villager", "villager", "villager");
        game.Spares = ["cupid", "villager"];
        game.Act(game.Seats[0].Id, true, new(game.PhaseId, "steal", ["0"]), Now);
        Assert.Equal("cupid", game.Step);
        Choose(game, 0, "love", 1, 3);
        Assert.Equal("wolves", game.Step);
        Assert.Contains("Du er forelsket i B.", game.Project(game.Seats[3].Id, false).PrivateMessages);
        Assert.Empty(game.Project(game.Seats[2].Id, false).PrivateMessages);
    }

    [Fact]
    public void Mandatory_thief_exchange_and_cupid_timeout_select_legal_choices()
    {
        var game = Scenario("thief", "thief", "cupid", "villager", "villager", "villager");
        game.Timed = true;
        game.DeadlineUtc = Now.AddSeconds(60);
        game.Spares = ["wolf", "wolf"];
        Assert.DoesNotContain(game.Project(game.Seats[0].Id, true).Actions, a => a.Key == "skip");
        Assert.True(game.AdvanceExpired(Now.AddSeconds(120)));
        Assert.Equal("wolf", game.Seats[0].Role);
        Assert.Equal(2, game.Lovers.Distinct().Count());
        Assert.Equal("wolves", game.Step);
        Assert.Equal(Now.AddSeconds(180), game.DeadlineUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Witch_can_resume_in_free_time_and_keeps_unused_potions_after_timeout(bool timed)
    {
        var game = Scenario("wolves", "wolf", "witch", "villager", "villager", "villager");
        game.Timed = timed;
        Choose(game, 0, "hunt", 2);
        Assert.Equal("heal", game.Step);
        var request = new SocialGameActionRequest(game.PhaseId, "heal", []);
        var fresh = JsonSerializer.Deserialize<WerewolfGame>(JsonSerializer.Serialize(game))!;
        Assert.Equal(timed, fresh.AdvanceExpired(Now.AddMinutes(4)));
        if (timed)
        {
            Assert.Equal("day", fresh.Step);
            Assert.True(fresh.HasHealing && fresh.HasPoison);
            Assert.False(fresh.Seats[2].Alive);
            Assert.Throws<WerewolfException>(() => fresh.Act(fresh.Seats[1].Id, false, request, Now.AddMinutes(4)));
        }
        else
        {
            fresh.Act(fresh.Seats[1].Id, false, request, Now.AddMinutes(4));
            Choose(fresh, 1, "poison", 3);
            Assert.True(fresh.Seats[2].Alive);
            Assert.False(fresh.Seats[3].Alive);
            Assert.False(fresh.HasHealing || fresh.HasPoison);
        }
    }

    [Fact]
    public void Guardian_protects_from_the_wolves_but_not_from_the_witch()
    {
        var game = Scenario("guard", "guard", "wolf", "villager", "witch", "villager");
        game.ProtectedLastNight = game.Seats[4].Id;
        Assert.Throws<WerewolfException>(() => Choose(game, 0, "protect", 4));
        Choose(game, 0, "protect", 2);
        Choose(game, 1, "hunt", 2);
        Assert.Equal("poison", game.Step);
        Choose(game, 3, "poison", 2);
        Assert.False(game.Seats[2].Alive);
        Assert.True(game.HasHealing);
    }

    [Fact]
    public void Wolves_receive_each_others_choices_and_disagreement_spares_the_village()
    {
        var game = Scenario("wolves", "wolf", "wolf", "villager", "villager");
        game.WolvesHaveMet = true;
        Choose(game, 0, "hunt", 2);
        Assert.Contains("A foreslår C.", game.Project(game.Seats[1].Id, false).PrivateMessages);
        Assert.Empty(game.Project(game.Seats[2].Id, true).PrivateMessages);
        Choose(game, 1, "hunt", 3);
        Assert.Equal("day", game.Step);
        Assert.All(game.Seats, p => Assert.True(p.Alive));
        Assert.Null(game.Result); // Equal teams do not end the original game.
    }

    [Fact]
    public void Lovers_hunter_takes_last_shot_before_the_winner_is_decided()
    {
        var game = Scenario("wolves", "wolf", "villager", "hunter");
        game.Lovers = [game.Seats[1].Id, game.Seats[2].Id];
        Choose(game, 0, "hunt", 1);
        Assert.Equal("hunter", game.Step);
        Assert.False(game.Seats[2].Alive);
        Assert.Null(game.Result);
        Choose(game, 2, "shoot", 0);
        Assert.Equal("finished", game.Step);
        Assert.Contains("ingen vinder", game.Result!);
    }

    [Fact]
    public void Mixed_lovers_win_together_and_dead_host_can_open_but_cannot_vote()
    {
        var game = Scenario("day", "wolf", "villager", "villager", "villager");
        var host = game.Seats[3].Id;
        game.Seats[3] = game.Seats[3] with { Alive = false };
        game.Lovers = [game.Seats[0].Id, game.Seats[1].Id];
        Assert.False(game.CanChat(host));
        game.Act(host, true, new(game.PhaseId, "open-vote", []), Now);
        Assert.Empty(game.Project(host, true).Actions);
        Assert.Throws<WerewolfException>(() => Choose(game, 0, "vote", 1));
        Choose(game, 0, "vote", 2);
        Assert.Throws<WerewolfException>(() => Choose(game, 0, "vote", 2));
        Choose(game, 1, "vote", 2);
        Choose(game, 2, "vote", 0);
        Assert.Equal("finished", game.Step);
        Assert.Contains("A og B", game.Result!);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(12, true)]
    [InlineData(40, false)]
    public void A_whole_party_can_play_from_deal_to_result_using_only_their_own_views(int count, bool timed)
    {
        var players = Enumerable.Range(1, count).Select(i => new SocialPlayer(Guid.NewGuid(), $"Nr. {i}", i % 2 == 0)).ToArray();
        string[] specials = count == 5 ? ["seer", "hunter"] : SocialRoles.Specials;
        var game = WerewolfGame.Start(players, new(specials, null, timed), Now);
        for (var tick = 0; tick < 250 && game.Result is null; tick++)
        {
            var phaseAtStart = game.PhaseId;
            foreach (var player in players)
            {
                var host = player.Id == players[0].Id;
                var view = game.Project(player.Id, host);
                if (view.Actions.Length == 0) continue;
                var action = view.Actions.First();
                var choices = action.Options.Take(action.MinChoices).Select(o => o.Value).ToArray();
                game.Act(player.Id, host, new(view.PhaseId, action.Key, choices), Now);
                if (phaseAtStart != game.PhaseId) break;
            }
            Assert.NotEqual(phaseAtStart, game.PhaseId);
        }
        Assert.NotNull(game.Result);
        Assert.All(players, player => Assert.True(game.Project(player.Id, false).Finished));
    }

    private static WerewolfGame Scenario(string step, params string[] roles) => new()
    {
        Step = step, PhaseId = 1, Round = 1,
        Seats = roles.Select((role, index) => new WerewolfSeat(Guid.NewGuid(), ((char)('A' + index)).ToString(), role)).ToArray()
    };

    private static void Choose(WerewolfGame game, int actor, string action, params int[] targets) =>
        game.Act(game.Seats[actor].Id, false, new(game.PhaseId, action, targets.Select(i => game.Seats[i].Id.ToString()).ToArray()), Now);
}
