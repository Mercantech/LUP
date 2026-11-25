using System.Reflection;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Pages.SocialGames;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class WerewolfCardGalleryTests
{
    [Fact]
    public void Public_gallery_has_separate_playable_upcoming_and_bonus_collections()
    {
        const string path = "/games/social/werewolf/cards";
        Assert.Contains(typeof(WerewolfCardGallery).GetCustomAttributes<RouteAttribute>(), r => r.Template == path);
        Assert.True(PublicPageAccess.Allows(path));
        Assert.False(PublicPageAccess.Allows(path + "/private"));
        Assert.False(PublicPageAccess.Allows("/games/social/werewolf/settings"));

        using var context = new BunitContext();
        var gallery = context.Render<WerewolfCardGallery>();
        var playable = gallery.FindAll("#playable .sg-gallery-name");
        Assert.Equal(SocialRoles.Names.Values.Order(), playable.Select(label => label.TextContent.Trim()).Order());
        Assert.Equal(16, gallery.FindAll("#karakters img").Count);
        Assert.Equal(2, gallery.FindAll("#bonus img").Count);
        Assert.Contains("Volle Maan", gallery.Find("#bonus p").TextContent);
        var images = gallery.FindAll(".sg-gallery-card img[loading='lazy']");
        Assert.Equal(26, images.Select(img => img.GetAttribute("src")).Distinct().Count());
        Assert.Empty(gallery.FindAll("input, dialog"));
    }

    [Fact]
    public async Task English_viewer_browses_across_collections_with_arrow_keys_and_buttons()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/modal-dialog.js");
        module.SetupVoid("open", _ => true).SetVoidResult();
        var gallery = context.Render<WerewolfCardGallery>(p => p.AddCascadingValue(new SocialGameLanguage(true)));
        gallery.Find("#playable li:last-child button").Click();
        Assert.Equal("Protector", gallery.Find("dialog h2").TextContent);
        Assert.Contains("golden leaves", gallery.Find("dialog img").GetAttribute("alt"));
        Assert.Contains("Protect one player", gallery.Find(".sg-guide-title p").TextContent);

        gallery.Find("dialog").KeyDown("ArrowRight");
        Assert.Equal("The Trusted Villager", gallery.Find("dialog h2").TextContent);
        Assert.Contains("#page=4", gallery.Find(".sg-guide-source").GetAttribute("href"));
        gallery.Find("dialog footer button:first-child").Click();
        Assert.Equal("Protector", gallery.Find("dialog h2").TextContent);
        gallery.Find("dialog footer button:last-child").Click();
        Assert.Equal("Upcoming roles", gallery.Find("dialog header span").TextContent);
        await gallery.Find("dialog").TriggerEventAsync("onclose", EventArgs.Empty);
        Assert.Empty(gallery.FindAll("dialog"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_card_has_localised_rules_an_illustration_and_a_distinct_example(bool english)
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/modal-dialog.js");
        module.SetupVoid("open", _ => true).SetVoidResult();
        var gallery = context.Render<WerewolfCardGallery>(p => p.AddCascadingValue(new SocialGameLanguage(english)));
        var names = gallery.FindAll(".sg-gallery-name").Select(label => label.TextContent).ToArray();
        gallery.Find(".sg-gallery-card").Click();
        var examples = new HashSet<string>();
        foreach (var name in names)
        {
            Assert.Equal(name, gallery.Find("dialog h2").TextContent);
            Assert.False(string.IsNullOrWhiteSpace(gallery.Find(".sg-guide-art").GetAttribute("alt")));
            Assert.Equal(english ? "Your goal" : "Dit mål", gallery.Find(".sg-guide-rules dt").TextContent);
            var rules = gallery.FindAll(".sg-guide-rules dd");
            Assert.Equal(3, rules.Count);
            Assert.All(rules, rule => Assert.False(string.IsNullOrWhiteSpace(rule.TextContent)));
            var example = gallery.Find(".sg-guide-example p").TextContent;
            Assert.False(string.IsNullOrWhiteSpace(example));
            Assert.True(examples.Add(example));
            gallery.Find("dialog footer button:last-child").Click();
        }
        Assert.Equal(26, examples.Count);
        Assert.Equal(names[0], gallery.Find("dialog h2").TextContent);
    }
}
