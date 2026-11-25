namespace Mercantec.Tech.Contracts;

public sealed record SocialGameSettings(string[] SpecialRoles, int? Wolves, bool Timed);
public sealed record SocialJoinRequest(string? Nickname);
public sealed record SocialChatRequest(string Text);
public sealed record SocialGameActionRequest(int PhaseId, string Action, string[] Choices);
public sealed record SocialRoomAccess(string Code, string? GuestKey, SocialRoomView Room);
public sealed record SocialPlayer(Guid Id, string Name, bool IsGuest);
public sealed record SocialChatLine(Guid Id, string Sender, string Text, DateTimeOffset SentAt);
public sealed record SocialRoomView(string Code, Guid Me, bool IsHost, SocialPlayer[] Players,
    SocialGameSettings Settings, int SuggestedWolves, string? StartProblem,
    WerewolfView? Game, SocialChatLine[] Chat, bool CanChat);
public sealed record WerewolfOption(string Value, string Label);
public sealed record WerewolfAction(string Key, string Label, int MinChoices, int MaxChoices, WerewolfOption[] Options);
public sealed record WerewolfView(int PhaseId, string Phase, int Round, string Role, Guid[] Alive,
    string PublicMessage, string[] PrivateMessages, DateTimeOffset? Deadline, WerewolfAction[] Actions,
    bool Finished, string? Result);
public sealed record SocialInviteCandidate(Guid UserSub, string Name, bool OwnHold);
public sealed record SocialInviteRequest(Guid UserSub);

public static class SocialRoles
{
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["villager"] = "Landsbyboer", ["wolf"] = "Varulv", ["thief"] = "Tyv", ["seer"] = "Seer",
        ["cupid"] = "Amor", ["hunter"] = "Jæger", ["witch"] = "Heks", ["guard"] = "Beskytter"
    };

    public static readonly string[] Specials = ["thief", "seer", "cupid", "hunter", "witch", "guard"];

    public static int SuggestedWolves(int players) => players switch
    {
        <= 7 => 1, <= 11 => 2, <= 17 => 3, _ => Math.Max(4, (players + 2) / 5)
    };
}
