using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Web.HomeGame;

public static class HomeRoomArt
{
    public static string? Image(string definition) => definition switch
    {
        HomeCatalog.DeskId => "images/home-workshop/desk.webp",
        HomeCatalog.SofaId => "images/home-workshop/sofa.webp",
        HomeCatalog.RugId => "images/home-workshop/rug.webp",
        HomeCatalog.LampId => "images/home-workshop/lamp.webp",
        _ => null
    };
}
