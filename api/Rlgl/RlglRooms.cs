using System.Security.Cryptography;
using System.Text;

namespace Mercantec.Tech.Api.Rlgl;

public sealed record RlglAdmission(string Code, string DeviceKey, string ViewKey);
public sealed record RlglSettings(double Threshold = 30, double Alpha = 0.2, int IntervalMs = 750);
public sealed record RlglControl(string Light, bool Reset = false, RlglSettings? Settings = null);
public sealed record RlglTelemetry(double? Rssi, double? RawRssi, int Samples, bool Fresh, int? SignalAgeMs);
public sealed record RlglPlayerView(int Number, double? Rssi, double? RawRssi, bool Fresh, bool Online, int? Place);
public sealed record RlglSnapshot(string Code, string Light, int Round, RlglSettings Settings, RlglPlayerView[] Players);
public sealed record RlglDeviceState(string Light, int Round, double Alpha, int IntervalMs, double Threshold, int? Place);

public sealed class RlglRooms
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.Ordinal);
    private const int RoomLimit = 32, PlayerLimit = 32;
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(4);
    private sealed class Room(Guid owner)
    {
        public readonly Guid Owner = owner;
        public readonly string Code = Secret(6);
        public readonly string DeviceKey = Secret(24);
        public readonly string ViewKey = Secret(24);
        public readonly Dictionary<string, Player> Players = new(StringComparer.Ordinal);
        public DateTimeOffset Touched = DateTimeOffset.UtcNow;
        public string Light = "idle";
        public int Round = 1, Winners;
        public RlglSettings Settings = new();
    }
    private sealed class Player(int number)
    {
        public readonly int Number = number;
        public Guid Session;
        public DateTimeOffset Seen = DateTimeOffset.MinValue;
        public double? Rssi, Raw;
        public bool Fresh;
        public int? Place;
    }
    private static string Secret(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes));
    private static bool Matches(string expected, string? actual) =>
        actual is not null && actual.Length == expected.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));
    private void Prune(DateTimeOffset now)
    {
        foreach (var key in _rooms.Where(x => now - x.Value.Touched > Lifetime).Select(x => x.Key).ToArray())
            _rooms.Remove(key);
    }

    public RlglAdmission? Create(Guid owner)
    {
        lock (_gate)
        {
            Prune(DateTimeOffset.UtcNow);
            if (_rooms.Count >= RoomLimit || _rooms.Values.Count(x => x.Owner == owner) >= 4) return null;
            var room = new Room(owner);
            _rooms.Add(room.Code, room);
            return new(room.Code, room.DeviceKey, room.ViewKey);
        }
    }

    public bool Control(string code, Guid owner, RlglControl control)
    {
        if (control.Light is not ("idle" or "red" or "green")) return false;
        if (control.Settings is { } s &&
            (!double.IsFinite(s.Threshold) || s.Threshold is < 15 or > 90 ||
             !double.IsFinite(s.Alpha) || s.Alpha is < 0.05 or > 1 ||
             s.IntervalMs is < 500 or > 2000)) return false;
        lock (_gate)
        {
            Prune(DateTimeOffset.UtcNow);
            if (!_rooms.TryGetValue(code, out var room) || room.Owner != owner) return false;
            if (control.Settings is not null) room.Settings = control.Settings;
            if (control.Reset)
            {
                room.Round++;
                room.Winners = 0;
                foreach (var player in room.Players.Values) { player.Place = null; player.Fresh = false; }
            }
            room.Light = control.Light;
            room.Touched = DateTimeOffset.UtcNow;
            return true;
        }
    }

    public bool JoinDevice(string code, string? key, string? id, Guid session)
    {
        if (id is null || id.Length != 12 || !id.All(Uri.IsHexDigit)) return false;
        lock (_gate)
        {
            Prune(DateTimeOffset.UtcNow);
            if (!_rooms.TryGetValue(code, out var room) || !Matches(room.DeviceKey, key)) return false;
            if (!room.Players.TryGetValue(id, out var player))
            {
                if (room.Players.Count >= PlayerLimit) return false;
                player = new Player(room.Players.Count + 1);
                room.Players.Add(id, player);
            }
            player.Session = session;
            player.Fresh = false;
            player.Seen = DateTimeOffset.MinValue;
            return true;
        }
    }

    public RlglDeviceState? Record(string code, string id, Guid session, RlglTelemetry telemetry, DateTimeOffset now)
    {
        if (telemetry.Samples is < 0 or > 100 || telemetry.SignalAgeMs is < 0 ||
            !Valid(telemetry.Rssi) || !Valid(telemetry.RawRssi)) return null;
        lock (_gate)
        {
            Prune(now);
            if (!_rooms.TryGetValue(code, out var room) || !room.Players.TryGetValue(id, out var player) ||
                player.Session != session) return null;
            // Reject rapid updates, not the connection. Device should normally send every 850+ ms.
            if (now - player.Seen < TimeSpan.FromMilliseconds(200))
                return State(room, player);
            player.Seen = now;
            player.Rssi = telemetry.SignalAgeMs is >= 0 and < 2000 ? telemetry.Rssi : null;
            player.Raw = telemetry.RawRssi;
            player.Fresh = telemetry.Fresh && telemetry.Samples > 0 && telemetry.SignalAgeMs is >= 0 and < 2000 && player.Rssi.HasValue;
            if (room.Light == "green" && player.Place is null && player.Fresh &&
                player.Rssi is double rssi && -rssi < room.Settings.Threshold)
                player.Place = ++room.Winners;
            room.Touched = now;
            return State(room, player);
        }
    }
    private static RlglDeviceState State(Room room, Player player) =>
        new(room.Light, room.Round, room.Settings.Alpha, room.Settings.IntervalMs, room.Settings.Threshold, player.Place);
    private static bool Valid(double? value) => !value.HasValue || (double.IsFinite(value.Value) && value.Value is >= -127 and < 0);

    public RlglSnapshot? Snapshot(string code, string? key, DateTimeOffset now)
    {
        lock (_gate)
        {
            Prune(now);
            if (!_rooms.TryGetValue(code, out var room) || !Matches(room.ViewKey, key)) return null;
            return new(room.Code, room.Light, room.Round, room.Settings,
                room.Players.Values.Select(p =>
                {
                    var online = now - p.Seen < TimeSpan.FromSeconds(5);
                    var fresh = online && p.Fresh && now - p.Seen < TimeSpan.FromSeconds(2);
                    return new RlglPlayerView(p.Number, online ? p.Rssi : null, online ? p.Raw : null, fresh, online, p.Place);
                }).OrderBy(p => p.Place ?? int.MaxValue).ThenByDescending(p => p.Rssi ?? -128).ToArray());
        }
    }
}
