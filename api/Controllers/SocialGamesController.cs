using System.Security.Claims;
using System.Security.Cryptography;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.SocialGames;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Route("api/social-games/rooms"), SocialRoomErrors]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class SocialGamesController(SocialRoomStore rooms, AppDbContext db) : ControllerBase
{
    private Guid? Subject => User.Identity?.IsAuthenticated is true ? UserClaims.GetSub(User) : null;
    private string? GuestKey => Subject is null && Request.Headers.TryGetValue("X-Guest-Key", out var value)
        ? value.ToString() : null;

    [HttpPost, Authorize]
    public async Task<ActionResult<SocialRoomAccess>> Create(SocialGameSettings settings, CancellationToken ct)
    {
        if (Subject is not Guid sub) return Unauthorized();
        var lobby = new SocialLobby { Settings = SocialLobby.CheckSettings(settings) };
        var host = new SocialSeat(Guid.NewGuid(), sub, null, await AccountName(sub, ct));
        lobby.Seats.Add(host);
        var room = await rooms.Create(sub, lobby, ct);
        return new SocialRoomAccess(room.Code, null, lobby.View(room, host));
    }

    [HttpGet("{code}"), AllowAnonymous]
    public Task<SocialRoomView> State(string code, CancellationToken ct) =>
        rooms.Read(code, (room, state) => state.View(room, state.RequireSeat(Subject, GuestKey)), ct);

    [HttpPost("{code}/join"), AllowAnonymous, EnableRateLimiting(SocialGameServices.JoinPolicy)]
    public async Task<SocialRoomAccess> Join(string code, SocialJoinRequest request, CancellationToken ct)
    {
        var sub = Subject;
        var guestKey = GuestKey;
        var joinKey = sub is null && Request.Headers.TryGetValue("X-Guest-Join-Key", out var chosen) ? chosen.ToString() : null;
        if (joinKey is not null && !SocialLobby.IsKey(joinKey))
            throw new SocialRoomException(400, "Gæstenøglen er ugyldig.");
        var accountName = sub is Guid user && guestKey is null ? await AccountName(user, ct) : null;
        return await rooms.Change<SocialRoomAccess>(code, (room, state, _) =>
        {
            var resumeKey = guestKey ?? joinKey;
            if (state.FindSeat(sub, resumeKey) is { } existing)
                return new(new(room.Code, resumeKey, state.View(room, existing)), false);
            if (guestKey is not null)
                throw new SocialRoomException(403, "Denne gæstenøgle giver ikke adgang til lobbyen.");
            state.RequireWaiting();
            if (state.Seats.Count >= WerewolfGame.MaxPlayers) throw new SocialRoomException(409, "Lobbyen er fuld.");
            string name;
            if (accountName is not null)
                name = UniqueAccountName(state, accountName);
            else
            {
                name = SocialLobby.Nickname(request.Nickname)
                    ?? throw new SocialRoomException(400, "Skriv et kaldenavn på 1–30 tegn med mindst ét bogstav eller tal.");
                if (NameTaken(state, name)) throw new SocialRoomException(409, "Kaldenavnet er optaget i denne lobby.");
            }
            var key = sub is null ? joinKey ?? RandomNumberGenerator.GetHexString(64) : null;
            var seat = new SocialSeat(Guid.NewGuid(), sub, key is null ? null : SocialLobby.HashKey(key), name);
            state.Seats.Add(seat);
            return new(new(room.Code, key, state.View(room, seat)));
        }, ct, markInvitation: guestKey is null ? sub : null);
    }

    [HttpPut("{code}/settings"), Authorize]
    public Task<SocialRoomView> Settings(string code, SocialGameSettings request, CancellationToken ct) =>
        HostCommand(code, (state, _) =>
        {
            state.RequireWaiting();
            state.Settings = SocialLobby.CheckSettings(request);
        }, ct);

    [HttpPost("{code}/start"), Authorize]
    public Task<SocialRoomView> Start(string code, CancellationToken ct) =>
        HostCommand(code, (state, now) =>
        {
            state.RequireWaiting();
            state.Game = WerewolfGame.Start(state.Seats.Select(s => s.PublicPlayer).ToArray(), state.Settings, now);
        }, ct);

    [HttpPost("{code}/actions"), AllowAnonymous]
    public Task<SocialRoomView> Act(string code, SocialGameActionRequest request, CancellationToken ct) =>
        rooms.Change<SocialRoomView>(code, (room, state, now) =>
        {
            var seat = state.RequireSeat(Subject, GuestKey);
            if (state.Game is null) throw new SocialRoomException(409, "Spillet er endnu ikke startet.");
            if (string.IsNullOrEmpty(request.Action) || request.Choices is null || request.Choices.Length > 8
                || request.Choices.Any(c => c is null || c.Length > 100))
                throw new SocialRoomException(400, "Spilhandlingen er ugyldig.");
            state.Game.Act(seat.Id, seat.UserSub == room.HostSub, request, now);
            return new(state.View(room, seat));
        }, ct);

    [HttpPost("{code}/chat"), AllowAnonymous]
    public Task<SocialRoomView> Chat(string code, SocialChatRequest request, CancellationToken ct) =>
        rooms.Change<SocialRoomView>(code, (room, state, now) =>
        {
            var seat = state.RequireSeat(Subject, GuestKey);
            var text = request.Text?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > 500 || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
                throw new SocialRoomException(400, "Skriv en besked på højst 500 tegn.");
            var messageId = Guid.TryParse(Request.Headers["Idempotency-Key"], out var id) && id != Guid.Empty ? id : Guid.NewGuid();
            if (state.Chat.Find(line => line.Id == messageId) is { } previous)
            {
                if (previous.Sender != seat.Name || previous.Text != text)
                    throw new SocialRoomException(409, "Beskedens id er allerede i brug.");
                return new(state.View(room, seat), false);
            }
            if (state.Game?.CanChat(seat.Id) == false)
                throw new SocialRoomException(403, "Du kan ikke skrive i landsbyens chat i denne fase.");
            if (state.Chat.Count(line => line.Sender == seat.Name && line.SentAt > now.AddSeconds(-10)) >= 10)
                throw new SocialRoomException(429, "Vent et øjeblik, før du skriver igen.");
            state.Chat.Add(new(messageId, seat.Name, text, now));
            if (state.Chat.Count > 150) state.Chat.RemoveAt(0);
            return new(state.View(room, seat));
        }, ct);

    private Task<SocialRoomView> HostCommand(string code, Action<SocialLobby, DateTimeOffset> run,
        CancellationToken ct) =>
        rooms.Change<SocialRoomView>(code, (room, state, now) =>
        {
            var seat = state.RequireSeat(Subject, GuestKey);
            if (seat.UserSub != room.HostSub) throw new SocialRoomException(403, "Kun værten kan gøre dette.");
            run(state, now);
            return new(state.View(room, seat));
        }, ct);

    private async Task<string> AccountName(Guid sub, CancellationToken ct)
    {
        var profile = await db.ProfileNames.AsNoTracking().Where(p => p.UserSub == sub)
            .Select(p => p.DisplayName).SingleOrDefaultAsync(ct);
        var name = UserClaims.CleanEditorName(profile) ?? UserClaims.EditorName(User)
            ?? UserClaims.CleanEditorName(User.FindFirstValue("given_name")) ?? "Spiller";
        return name.Length <= 30 ? name : name[..(char.IsHighSurrogate(name[29]) ? 29 : 30)];
    }

    private static string UniqueAccountName(SocialLobby state, string name)
    {
        var candidate = name;
        for (var suffix = 2; NameTaken(state, candidate); suffix++)
            candidate = $"{name} ({suffix})";
        return candidate;
    }

    private static bool NameTaken(SocialLobby state, string name) =>
        state.Seats.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

internal sealed class SocialRoomErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            SocialRoomException room => room.Status,
            WerewolfException game => game.Status,
            _ => (int?)null
        };
        if (status is null) return;
        context.Result = new ObjectResult(new { error = context.Exception.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
