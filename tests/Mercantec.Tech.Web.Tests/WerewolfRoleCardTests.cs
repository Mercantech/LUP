using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Pages.SocialGames;

namespace Mercantec.Tech.Web.Tests;

public sealed class WerewolfRoleCardTests
{
    [Fact]
    public void Choosing_an_illustrated_special_keeps_existing_selections()
    {
        using var context = new BunitContext();
        string[] chosen = [];
        var picker = context.Render<WerewolfRolePicker>(p => p
            .Add(c => c.Selected, ["seer"])
            .Add(c => c.Editable, true)
            .Add(c => c.SelectionChanged, roles => chosen = roles));

        foreach (var role in SocialRoles.Specials)
        {
            var label = picker.Find($"input[value='{role}']").Closest("label");
            Assert.Equal($"/images/social-games/roles/{role}.webp", label?.QuerySelector("img")?.GetAttribute("src"));
        }
        picker.Find("input[value='witch']").Change(true);
        Assert.Equal(["seer", "witch"], chosen);
    }

    [Theory]
    [InlineData("wolf", "werewolf.webp")]
    [InlineData("villager", "villager.webp")]
    [InlineData("thief", "thief.webp")]
    [InlineData("seer", "seer.webp")]
    [InlineData("cupid", "cupid.webp")]
    [InlineData("hunter", "hunter.webp")]
    [InlineData("witch", "witch.webp")]
    [InlineData("guard", "guard.webp")]
    public void Only_the_players_own_illustration_and_private_message_appear_when_the_card_is_open(string role, string file)
    {
        const string secret = "Din private besked fra natten.";
        const string art = "/images/social-games/roles/";
        var me = new SocialPlayer(Guid.NewGuid(), "Sofie", false);
        var guest = new SocialPlayer(Guid.NewGuid(), "Martin", true);
        var game = new WerewolfView(7, "night", 1, role, [me.Id, guest.Id], "Natten er begyndt.", [secret],
            null, [], false, null);
        var room = new SocialRoomView("042173", me.Id, true, [me, guest], new(SocialRoles.Specials, 1, true), 1,
            null, game, [], false);
        using var context = new BunitContext();
        var page = context.Render<WerewolfRound>(p => p.Add(c => c.Room, room));
        const string toggle = "button[aria-controls='sg-private-info']";

        Assert.DoesNotContain(art, page.Markup);
        Assert.DoesNotContain(secret, page.Markup);

        page.Find(toggle).Click();
        Assert.Single(page.FindAll($"img[src^='{art}']"));
        var card = page.Find("#sg-private-info");
        Assert.Equal(art + file, card.QuerySelector("img")?.GetAttribute("src"));
        Assert.Contains(SocialRoles.Names[role], card.TextContent);
        Assert.Contains(secret, card.TextContent);

        page.Find(toggle).Click();
        Assert.DoesNotContain(art, page.Markup);
        Assert.DoesNotContain(secret, page.Markup);
    }
}
