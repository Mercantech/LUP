using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Web.HomeGame;

public static class HomeRoomArt
{
    public static string? Image(string definition, bool? powered = null, int rotation = 0)
    {
        var sprite = definition switch
        {
            HomeCatalog.DeskId => "desk",
            HomeCatalog.SofaId => "sofa",
            HomeCatalog.RugId => "rug",
            HomeCatalog.LampId => "lamp",
            HomeCatalog.BlindId => "blind",
            HomeCatalog.ArmchairId => "armchair",
            HomeCatalog.BookcaseId => "bookcase",
            HomeCatalog.CoffeeTableId => "coffee-table",
            HomeCatalog.CorkboardId => "pinboard",
            HomeCatalog.SignId when powered is false => "flip-sign-moon",
            HomeCatalog.SignId => "flip-sign",
            HomeCatalog.PlanterId => "planter",
            HomeCatalog.MoistureId => "soil-probe",
            _ => null
        };
        if (sprite is null) return null;
        var directional = definition is HomeCatalog.DeskId or HomeCatalog.SofaId or HomeCatalog.LampId
            or HomeCatalog.ArmchairId or HomeCatalog.BookcaseId or HomeCatalog.CoffeeTableId
            or HomeCatalog.PlanterId or HomeCatalog.MoistureId;
        var angle = directional ? rotation : 0;
        var view = angle switch { 180 => "-back", 90 or 270 => "-side", _ => "" };
        return $"images/home-workshop/{sprite}{view}.webp";
    }
}
