using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Web.HomeGame;

public static class HomeReadingPoint
{
    public static (HomeCell Cell, int Lux)? Measure(HomeLayout layout, HomeWorldSnapshot? snapshot)
    {
        if (snapshot is null) return null;
        if (snapshot is { Activity: "reading", AvatarCell: { } reader, AvatarLux: int lux })
            return (reader, lux);

        var desk = layout.Items.FirstOrDefault(item =>
            item.Role == HomeCatalog.PrimaryDeskRole && item.Placement is not null);
        if (desk is null || snapshot.LightMap.Count == 0) return null;
        var path = HomeRules.FindPath(layout, snapshot.AvatarCell ?? layout.Grid.Entry,
            HomeRules.AccessibleInteractions(layout, desk));
        if (path is not { Count: > 0 }) return null;
        var reading = snapshot.LightMap.FirstOrDefault(sample => sample.Cell == path[^1]);
        return reading is null ? null : (reading.Cell, reading.Lux);
    }
}
