using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.SocialGames;

public sealed record SocialSeat(Guid Id, Guid? UserSub, string? GuestHash, string Name)
{
    [JsonIgnore] public SocialPlayer PublicPlayer => new(Id, Name, UserSub is null);
}

public sealed class SocialLobby
{
    public required SocialGameSettings Settings { get; set; }
    public List<SocialSeat> Seats { get; set; } = [];
    public List<SocialChatLine> Chat { get; set; } = [];
    public WerewolfGame? Game { get; set; }

    public static SocialGameSettings CheckSettings(SocialGameSettings settings)
    {
        if (settings.SpecialRoles is null || settings.SpecialRoles.Length > SocialRoles.Specials.Length
            || settings.SpecialRoles.Any(role => !SocialRoles.Specials.Contains(role, StringComparer.Ordinal))
            || settings.SpecialRoles.Distinct(StringComparer.Ordinal).Count() != settings.SpecialRoles.Length)
            throw new SocialRoomException(400, "Vælg gyldige specialkort, højst ét af hver slags.");
        if (settings.Wolves is <= 0 or >= WerewolfGame.MaxPlayers)
            throw new SocialRoomException(400, "Vælg mindst én varulv og giv plads til landsbyen.");
        return settings;
    }

    public SocialSeat? FindSeat(Guid? sub, string? guestKey)
    {
        if (guestKey is null)
            return sub.HasValue ? Seats.Find(seat => seat.UserSub == sub) : null;
        if (!IsKey(guestKey)) return null;
        var hash = HashKey(guestKey);
        return Seats.Find(seat => seat.GuestHash == hash);
    }

    public static string HashKey(string key) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(key)));
    public static bool IsKey(string key) => key.Length == 64 && key.All(char.IsAsciiHexDigit);

    public static string? Nickname(string? raw)
    {
        if (raw is null || raw.Length > 120) return null;
        var name = raw.Trim().Normalize(NormalizationForm.FormKC);
        return name.Length is >= 1 and <= 30 && name.Any(char.IsLetterOrDigit)
            && !name.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format)
            ? name : null;
    }

    public SocialSeat RequireSeat(Guid? sub, string? guestKey) =>
        FindSeat(sub, guestKey) ?? throw new SocialRoomException(403, "Deltag i lobbyen for at åbne den.");

    public void RequireWaiting()
    {
        if (Game is not null) throw new SocialRoomException(409, "Spillet er allerede startet.");
    }

    public SocialRoomView View(SocialGameRoom room, SocialSeat viewer)
    {
        var isHost = viewer.UserSub == room.HostSub;
        return new(room.Code, viewer.Id, isHost, Seats.Select(s => s.PublicPlayer).ToArray(), Settings,
            SocialRoles.SuggestedWolves(Seats.Count), Game is null ? WerewolfGame.StartProblem(Seats.Count, Settings) : null,
            Game?.Project(viewer.Id, isHost), Chat.ToArray(), Game?.CanChat(viewer.Id) ?? true);
    }
}

public sealed class SocialRoomException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
