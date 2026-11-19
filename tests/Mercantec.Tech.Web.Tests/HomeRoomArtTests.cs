using Mercantec.Tech.Contracts.HomeGame;
using Mercantec.Tech.Web.HomeGame;

namespace Mercantec.Tech.Web.Tests;

public sealed class HomeRoomArtTests
{
    [Theory]
    [InlineData(HomeCatalog.DeskId, "desk")]
    [InlineData(HomeCatalog.SofaId, "sofa")]
    [InlineData(HomeCatalog.LampId, "lamp")]
    [InlineData(HomeCatalog.ArmchairId, "armchair")]
    [InlineData(HomeCatalog.BookcaseId, "bookcase")]
    [InlineData(HomeCatalog.CoffeeTableId, "coffee-table")]
    [InlineData(HomeCatalog.PlanterId, "planter")]
    [InlineData(HomeCatalog.MoistureId, "soil-probe")]
    public void Rotating_an_upright_item_uses_a_directional_view(string id, string file)
    {
        var baseName = $"images/home-workshop/0108ab341b3c/{file}";
        const string extension = ".webp";
        Assert.Equal(baseName + extension, HomeRoomArt.Image(id));
        Assert.Equal(baseName + "-back" + extension, HomeRoomArt.Image(id, rotation: 180));
        Assert.Equal(baseName + "-side" + extension, HomeRoomArt.Image(id, rotation: 90));
        var oppositeSide = id is HomeCatalog.DeskId or HomeCatalog.PlanterId or HomeCatalog.LampId ? "-side-reverse" : "-side";
        Assert.Equal(baseName + oppositeSide + extension, HomeRoomArt.Image(id, rotation: 270));
    }

    [Fact]
    public void Sign_keeps_the_commanded_symbol_on_each_wall()
    {
        foreach (var direction in new[] { 0, 90, 180, 270 })
        {
            Assert.Equal("images/home-workshop/0108ab341b3c/flip-sign-moon.webp", HomeRoomArt.Image(HomeCatalog.SignId, false, direction));
            Assert.Equal("images/home-workshop/0108ab341b3c/flip-sign.webp", HomeRoomArt.Image(HomeCatalog.SignId, true, direction));
        }
    }
}
