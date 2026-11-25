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
        var playable = gallery.FindAll("#playable button");
        Assert.Equal(SocialRoles.Names.Values.Order(), playable.Select(button => button.TextContent.Trim()).Order());
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
        Assert.Contains("Protect one player", gallery.Find("dialog figcaption p").TextContent);

        gallery.Find("dialog").KeyDown("ArrowRight");
        Assert.Equal("The Trusted Villager", gallery.Find("dialog h2").TextContent);
        Assert.Empty(gallery.FindAll("dialog figcaption p"));
        gallery.Find("dialog footer button:first-child").Click();
        Assert.Equal("Protector", gallery.Find("dialog h2").TextContent);
        gallery.Find("dialog footer button:last-child").Click();
        Assert.Equal("Upcoming roles", gallery.Find("dialog header span").TextContent);
        await gallery.Find("dialog").TriggerEventAsync("onclose", EventArgs.Empty);
        Assert.Empty(gallery.FindAll("dialog"));
    }
}
