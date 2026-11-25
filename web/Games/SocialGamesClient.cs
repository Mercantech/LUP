using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Games;

public sealed class SocialRoomException(HttpStatusCode status, string message) : InvalidOperationException(message)
{
    public HttpStatusCode Status { get; } = status;
}

public sealed class SocialGamesClient(HttpClient http, IJSRuntime js, AuthService auth)
{
    private const string Rooms = "social-games/rooms";
    private static string Path(string code) => $"{Rooms}/{Uri.EscapeDataString(code)}";
    private static string GuestSlot(string code) => $"lup.werewolf.guest.{code.ToUpperInvariant()}";
    private readonly Dictionary<string, string?> guestMemory = [];
    private (string Code, string Text, Guid Id)? chatAttempt;

    private async ValueTask<string?> ReadKey(string slot)
    {
        if (guestMemory.TryGetValue(slot, out var saved)) return saved;
        try { return guestMemory[slot] = await js.InvokeAsync<string?>("sessionStorage.getItem", slot); }
        catch (JSException) { return null; }
    }

    private async ValueTask WriteKey(string slot, string? key)
    {
        // Keep a seat usable in this running tab if browser storage is unavailable.
        guestMemory[slot] = key;
        try
        {
            if (key is null) await js.InvokeVoidAsync("sessionStorage.removeItem", slot);
            else await js.InvokeVoidAsync("sessionStorage.setItem", slot, key);
        }
        catch (JSException) { }
    }

    public async Task<SocialRoomView> CreateAsync(SocialGameSettings settings)
    {
        using var response = await SendAsync(HttpMethod.Post, Rooms, body: settings);
        return (await ReadAsync<SocialRoomAccess>(response)).Room;
    }

    public async Task<SocialRoomView> JoinAsync(string code, string? nickname)
    {
        var slot = GuestSlot(code);
        var pendingSlot = slot + ".pending";
        string? newKey = null;
        if (!auth.IsAuthenticated && await ReadKey(slot) is null)
        {
            // Save before sending. A lost admission response can then be replayed without creating a second seat.
            newKey = await ReadKey(pendingSlot) ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await WriteKey(pendingSlot, newKey);
        }
        using var response = await SendAsync(HttpMethod.Post, $"{Path(code)}/join", code, new SocialJoinRequest(nickname), joinKey: newKey);
        var joined = await ReadAsync<SocialRoomAccess>(response);
        if (!string.IsNullOrEmpty(joined.GuestKey))
        {
            await WriteKey(slot, joined.GuestKey);
            await WriteKey(pendingSlot, null);
        }
        return joined.Room;
    }

    public async Task<SocialRoomView> GetAsync(string code)
    {
        try
        {
            var view = await RoomAsync(HttpMethod.Get, code, "");
            var slot = GuestSlot(code);
            if (!auth.IsAuthenticated && await ReadKey(slot + ".pending") is { Length: > 0 } recovered)
            {
                await WriteKey(slot, recovered);
                await WriteKey(slot + ".pending", null);
            }
            return view;
        }
        catch (SocialRoomException ex) when (!auth.IsAuthenticated && ex.Status == HttpStatusCode.Forbidden)
        {
            // A six-digit code can eventually belong to a new room. Its old seat cannot be resumed.
            await WriteKey(GuestSlot(code), null);
            throw;
        }
    }
    public Task<SocialRoomView> SettingsAsync(string code, SocialGameSettings settings) => RoomAsync(HttpMethod.Put, code, "/settings", settings);
    public Task<SocialRoomView> StartAsync(string code) => RoomAsync(HttpMethod.Post, code, "/start");
    public Task<SocialRoomView> ActAsync(string code, SocialGameActionRequest action) => RoomAsync(HttpMethod.Post, code, "/actions", action);
    public async Task<SocialRoomView> ChatAsync(string code, string text)
    {
        var id = chatAttempt is { } previous && previous.Code == code && previous.Text == text
            ? previous.Id : Guid.NewGuid();
        chatAttempt = (code, text, id);
        using var response = await SendAsync(HttpMethod.Post, Path(code) + "/chat", code, new SocialChatRequest(text), messageId: id);
        var room = await ReadAsync<SocialRoomView>(response);
        chatAttempt = null;
        return room;
    }

    public async Task<SocialInviteCandidate[]> CandidatesAsync(string code, string query)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Path(code)}/invitees?q={Uri.EscapeDataString(query)}");
        return await ReadAsync<SocialInviteCandidate[]>(response);
    }

    public async Task InviteAsync(string code, Guid userSub)
    {
        using var response = await SendAsync(HttpMethod.Post, $"{Path(code)}/invitations", body: new SocialInviteRequest(userSub));
    }

    private async Task<SocialRoomView> RoomAsync(HttpMethod method, string code, string suffix, object? body = null)
    {
        using var response = await SendAsync(method, Path(code) + suffix, code, body);
        return await ReadAsync<SocialRoomView>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("Spilserveren sendte et tomt svar.");

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? code = null, object? body = null,
        string? joinKey = null, Guid? messageId = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        if (messageId.HasValue) request.Headers.Add("Idempotency-Key", messageId.Value.ToString());
        if (!auth.IsAuthenticated && code is not null)
        {
            if (joinKey is not null) request.Headers.Add("X-Guest-Join-Key", joinKey);
            else
            {
                var slot = GuestSlot(code);
                var key = await ReadKey(slot) ?? await ReadKey(slot + ".pending");
                if (!string.IsNullOrWhiteSpace(key)) request.Headers.Add("X-Guest-Key", key);
            }
        }
        var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.NotFound => "Lobbyen blev ikke fundet.",
                HttpStatusCode.Unauthorized => "Log ind eller deltag som gæst.",
                HttpStatusCode.Forbidden => "Du har ikke adgang til denne handling.",
                HttpStatusCode.TooManyRequests => "Vent et øjeblik, og prøv igen.",
                _ => "Handlingen kunne ikke gennemføres. Prøv igen."
            };
            try
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (json.ValueKind == JsonValueKind.Object && (json.TryGetProperty("error", out var detail) || json.TryGetProperty("message", out detail))
                    && detail.ValueKind == JsonValueKind.String)
                    message = detail.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new SocialRoomException(response.StatusCode, message);
        }
    }
}
