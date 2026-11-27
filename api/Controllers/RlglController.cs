using System.Net.WebSockets;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Rlgl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/rlgl")]
[Authorize]
public sealed class RlglController(RlglRooms rooms, IConfiguration configuration) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim Slots = new(128, 128);
    private sealed record Hello(string? Code, string? Key, string? DeviceId);

    [HttpPost("rooms")]
    public IActionResult Create()
    {
        if (!UserClaims.IsTeacher(User) || UserClaims.GetSub(User) is not Guid owner) return Forbid();
        var admission = rooms.Create(owner);
        return admission is null ? StatusCode(429) : Ok(admission);
    }

    [HttpPost("rooms/{code}/control")]
    public IActionResult Control(string code, [FromBody] RlglControl control)
    {
        if (!UserClaims.IsTeacher(User) || UserClaims.GetSub(User) is not Guid owner) return Forbid();
        return rooms.Control(code.ToUpperInvariant(), owner, control) ? NoContent() : BadRequest();
    }

    [AllowAnonymous, HttpGet("connect")]
    public Task Connect(CancellationToken ct) => Run(false, ct);
    [AllowAnonymous, HttpGet("live")]
    public Task Live(CancellationToken ct) => Run(true, ct);

    private async Task Run(bool viewer, CancellationToken ct)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest) { Response.StatusCode = 400; return; }
        var origin = Request.Headers.Origin.ToString();
        if (viewer && !AllowedOrigin(origin)) { Response.StatusCode = 403; return; }
        if (!viewer && !string.IsNullOrEmpty(origin)) { Response.StatusCode = 403; return; }
        if (!Slots.Wait(0)) { Response.StatusCode = 429; return; }
        try
        {
            using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            try
            {
                using var handshake = CancellationTokenSource.CreateLinkedTokenSource(ct);
                handshake.CancelAfter(TimeSpan.FromSeconds(5));
                var hello = await Receive<Hello>(socket, handshake.Token);
                if (hello?.Code is not { Length: 12 } code || !code.All(Uri.IsHexDigit)) { socket.Abort(); return; }
                code = code.ToUpperInvariant();
                if (viewer)
                {
                    if (rooms.Snapshot(code, hello.Key, DateTimeOffset.UtcNow) is null) { socket.Abort(); return; }
                    // A pending receive detects disconnects and allows protocol ping/pong processing.
                    using var viewCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var read = Receive<Hello>(socket, viewCt.Token);
                    try
                    {
                        while (socket.State == WebSocketState.Open && !read.IsCompleted && !ct.IsCancellationRequested)
                        {
                            var snapshot = rooms.Snapshot(code, hello.Key, DateTimeOffset.UtcNow);
                            if (snapshot is null) break;
                            await Send(socket, snapshot, ct);
                            await Task.Delay(500, ct);
                        }
                    }
                    finally
                    {
                        await viewCt.CancelAsync();
                        try { await read; } catch (Exception e) when (e is OperationCanceledException or WebSocketException or JsonException or InvalidDataException) { }
                    }
                }
                else
                {
                    var id = hello.DeviceId?.ToUpperInvariant();
                    var session = Guid.NewGuid();
                    if (id is null || !rooms.JoinDevice(code, hello.Key, id, session)) { socket.Abort(); return; }
                    while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeout.CancelAfter(TimeSpan.FromSeconds(20));
                        var telemetry = await Receive<RlglTelemetry>(socket, timeout.Token);
                        if (telemetry is null) break;
                        var state = rooms.Record(code, id, session, telemetry, DateTimeOffset.UtcNow);
                        if (state is null) break;
                        await Send(socket, state, timeout.Token);
                        await Task.Delay(100, ct);
                    }
                }
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException or JsonException or InvalidDataException) { }
            finally { socket.Abort(); }
        }
        finally { Slots.Release(); }
    }

    private bool AllowedOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return false;
        if (string.Equals(uri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase)) return true;
        return (configuration["Cors:SpaOrigins"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => string.Equals(value.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<T?> Receive<T>(WebSocket socket, CancellationToken ct) where T : class
    {
        var bytes = new byte[1024];
        var length = 0;
        while (true)
        {
            if (length == bytes.Length) throw new InvalidDataException("Message too large.");
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(bytes, length, bytes.Length - length), ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text) throw new InvalidDataException("Text expected.");
            length += result.Count;
            if (result.EndOfMessage) return JsonSerializer.Deserialize<T>(bytes.AsSpan(0, length), Json);
        }
    }
    private static Task Send<T>(WebSocket socket, T value, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(value, Json)), WebSocketMessageType.Text, true, ct);
}
