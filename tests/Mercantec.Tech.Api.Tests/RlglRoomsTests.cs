using Mercantec.Tech.Api.Rlgl;
using Xunit;

namespace Mercantec.Tech.Api.Tests;

public class RlglRoomsTests
{
    [Fact]
    public void TenPlayersHaveIndependentPlacesAndOnlyFreshGreenMeasurementsWin()
    {
        var rooms = new RlglRooms();
        var owner = Guid.NewGuid();
        var room = rooms.Create(owner)!;
        var now = DateTimeOffset.UtcNow;
        Assert.False(rooms.Control(room.Code, Guid.NewGuid(), new("green")));
        Assert.True(rooms.Control(room.Code, owner, new("green")));
        Assert.Null(rooms.Snapshot(room.Code, room.DeviceKey, now));
        for (var i = 0; i < 10; i++)
        {
            var id = i.ToString("X12");
            var session = Guid.NewGuid();
            Assert.False(rooms.JoinDevice(room.Code, room.ViewKey, id, session));
            Assert.True(rooms.JoinDevice(room.Code, room.DeviceKey, id, session));
            Assert.Null(rooms.Record(room.Code, id, Guid.NewGuid(), new(-20, -20, 1, true, 0), now));
            Assert.Null(rooms.Record(room.Code, id, session, new(-29, null, 0, false, 1000), now)!.Place);
            Assert.Null(rooms.Record(room.Code, id, session, new(-30, -30, 1, true, 0), now.AddSeconds(1))!.Place);
            Assert.Equal(i + 1, rooms.Record(room.Code, id, session, new(-29, -29, 1, true, 0), now.AddSeconds(2))!.Place);
            Assert.Equal(i + 1, rooms.Record(room.Code, id, session, new(-70, -70, 1, true, 0), now.AddSeconds(3))!.Place);
        }
        var view = rooms.Snapshot(room.Code, room.ViewKey, now.AddSeconds(3))!;
        Assert.Equal(10, view.Players.Length);
        Assert.All(view.Players, p => Assert.True(p.Online));
        Assert.True(rooms.Control(room.Code, owner, new("red", true)));
        Assert.All(rooms.Snapshot(room.Code, room.ViewKey, now.AddSeconds(3))!.Players, p => Assert.Null(p.Place));
    }

    [Fact]
    public void LostSignalAndRedLightDoNotWin()
    {
        var rooms = new RlglRooms();
        var owner = Guid.NewGuid();
        var room = rooms.Create(owner)!;
        var id = "123456789ABC";
        var session = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.True(rooms.JoinDevice(room.Code, room.DeviceKey, id, session));
        Assert.True(rooms.Control(room.Code, owner, new("red")));
        Assert.Null(rooms.Record(room.Code, id, session, new(-20, -20, 1, true, 0), now)!.Place);
        Assert.True(rooms.Control(room.Code, owner, new("green")));
        Assert.Null(rooms.Record(room.Code, id, session, new(-20, -20, 1, true, 3000), now.AddSeconds(1))!.Place);
        Assert.False(rooms.Snapshot(room.Code, room.ViewKey, now.AddSeconds(10))!.Players.Single().Online);
    }
}
