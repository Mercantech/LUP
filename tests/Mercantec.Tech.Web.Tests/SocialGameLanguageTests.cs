using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Layout;
using Mercantec.Tech.Web.Pages.SocialGames;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class SocialGameLanguageTests
{
    private const string StorageKey = "lup.social-games.language";

    [Fact]
    public void Language_is_saved_and_restored_when_the_game_layout_is_recreated()
    {
        using var first = new BunitContext();
        first.JSInterop.Setup<string?>("localStorage.getItem", StorageKey).SetResult(null);
        first.JSInterop.SetupVoid("localStorage.setItem", StorageKey, "en").SetVoidResult();
        var layout = first.Render<SocialGameLayout>();
        layout.WaitForAssertion(() => Assert.Equal("true", layout.Find("button[lang=da]").GetAttribute("aria-pressed")));
        layout.Find("button[lang=en]").Click();
        layout.WaitForAssertion(() => Assert.Equal("true", layout.Find("button[lang=en]").GetAttribute("aria-pressed")));

        var write = Assert.Single(first.JSInterop.Invocations["localStorage.setItem"]);
        Assert.Equal(StorageKey, write.Arguments[0]);
        using var reloaded = new BunitContext();
        reloaded.JSInterop.Setup<string?>("localStorage.getItem", StorageKey).SetResult(Assert.IsType<string>(write.Arguments[1]));
        var restored = reloaded.Render<SocialGameLayout>();
        restored.WaitForAssertion(() =>
        {
            Assert.Equal("true", restored.Find("button[lang=en]").GetAttribute("aria-pressed"));
            Assert.Equal("false", restored.Find("button[lang=da]").GetAttribute("aria-pressed"));
        });
    }

    [Fact]
    public void Language_switch_preserves_the_private_card_and_the_unsubmitted_target()
    {
        var me = new SocialPlayer(Guid.NewGuid(), "Alma", true);
        var target = new SocialPlayer(Guid.NewGuid(), "Jæger", false);
        var action = new WerewolfAction("poison", "Giv gift til en spiller", 1, 1,
            [new(target.Id.ToString(), target.Name)]);
        var game = new WerewolfView(6, "night", 1, "witch", [me.Id, target.Id], "Den første nat er begyndt.",
            [], null, [action], false, null);
        var room = new SocialRoomView("042173", me.Id, false, [me, target], new(["witch"], 1, false), 1,
            null, game, [], false);
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("localStorage.getItem", StorageKey).SetResult(null);
        context.JSInterop.SetupVoid("localStorage.setItem", _ => true).SetVoidResult();
        var layout = context.Render<SocialGameLayout>(p => p.Add(c => c.Body, (RenderFragment)(body =>
        {
            body.OpenComponent<WerewolfRound>(0);
            body.AddAttribute(1, nameof(WerewolfRound.Room), room);
            body.CloseComponent();
        })));

        const string card = "#sg-private-info";
        const string toggle = "button[aria-controls='sg-private-info']";
        layout.Find(toggle).Click();
        Assert.Equal("Heks", layout.Find($"{card} h3").TextContent);
        layout.Find($"{card} input[type=radio]").Change(true);
        layout.Find("button[lang=en]").Click();
        layout.WaitForAssertion(() =>
        {
            Assert.Equal("Witch", layout.Find($"{card} h3").TextContent);
            Assert.Equal(target.Name, layout.Find($"{card} .sg-options label").TextContent.Trim());
            Assert.True(layout.Find($"{card} input[type=radio]").HasAttribute("checked"));
            Assert.False(layout.Find($"{card} .sg-action button").HasAttribute("disabled"));
        });
        layout.Find(toggle).Click();
        layout.Find("button[lang=da]").Click();
        Assert.Empty(layout.FindAll(card));
    }
}
