using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.HomeGame;

public sealed class HomeGameOptions
{
    public bool Enabled { get; set; } = true;
    public string? RunnerUrl { get; set; }
    public string? RunnerToken { get; set; }
    public int WorkerSlots { get; set; } = 8;
}

internal sealed record HomeRunnerError(string Code, string Message);
internal sealed record HomeRunnerSession(Guid? SessionId, HomeRunnerError? Error);
internal sealed record HomeRunnerCommand(
    [property: JsonRequired] string Alias,
    [property: JsonRequired] string Action,
    [property: JsonRequired] bool Value);
internal sealed record HomeRunnerBatch(
    [property: JsonRequired] Guid RunId,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] long Sequence,
    [property: JsonRequired] int Tick,
    IReadOnlyList<HomeRunnerCommand>? Commands,
    double? NextDelayMs,
    IReadOnlyList<string>? Logs,
    bool Done,
    HomeRunnerError? Error);

internal sealed class HomeRunnerException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class HomeRunnerClient(IHttpClientFactory clients, IOptions<HomeGameOptions> options)
{
    private const int MaxReplyBytes = 96 * 1024;
    private Uri? BaseUri
    {
        get
        {
            var url = options.Value.RunnerUrl?.Trim().TrimEnd('/');
            return Uri.TryCreate(url + "/", UriKind.Absolute, out var baseUri)
                && (baseUri.Scheme == Uri.UriSchemeHttps || baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback)
                && baseUri.UserInfo.Length == 0 && baseUri.Query.Length == 0 && baseUri.Fragment.Length == 0
                && options.Value.RunnerToken is { Length: >= 32 }
                    ? baseUri : null;
        }
    }

    internal async Task<bool> IsAvailable(CancellationToken ct)
    {
        if (BaseUri is null) return false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var response = await Send(HttpMethod.Get, "health", null, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception error) when (error is HomeRunnerException or HttpRequestException or OperationCanceledException)
        {
            return false;
        }
    }

    internal async Task Start(Guid sessionId, Guid runId, string controllerId, HomeProgramDraft program,
        IReadOnlyDictionary<string, string> aliases, CancellationToken ct)
    {
        var body = Content(new { sessionId, runId, controllerId, program.Language, program.Source, aliases });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        using var response = await Send(HttpMethod.Post, "sessions", body, timeout.Token);
        var session = await Read<HomeRunnerSession>(response, timeout.Token);
        if (session.Error is not null) throw Failure(session.Error);
        if (session.SessionId != sessionId) throw InvalidReply();
    }

    internal async Task<HomeRunnerBatch> Activate(Guid sessionId, Guid runId, string controllerId,
        long sequence, HomeFrame frame, CancellationToken ct)
    {
        var values = frame.Sensors.ToDictionary(sensor => sensor.Alias, sensor => sensor.Value, StringComparer.Ordinal);
        var body = Content(new { runId, controllerId, sequence, frame.Tick, frame.TimeMs, values });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(7));
        using var response = await Send(HttpMethod.Post, $"sessions/{sessionId}/activate", body, timeout.Token);
        var batch = await Read<HomeRunnerBatch>(response, timeout.Token);
        if (batch.RunId != runId || batch.ControllerId != controllerId || batch.Sequence != sequence
            || batch.Tick != frame.Tick)
            throw InvalidReply();
        if (batch.Error is not null) throw Failure(batch.Error);
        if (batch.Commands is null || batch.Commands.Count > 256
            || batch.Commands.Any(command => command is null || command.Alias is not { Length: > 0 and <= 48 }
                || command.Action != "setPower")
            || batch.Logs?.Any(log => log is null) == true
            || batch.Done == false && batch.NextDelayMs is null)
            throw InvalidReply();
        return batch;
    }

    internal async Task Stop(Guid sessionId)
    {
        if (BaseUri is null) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var response = await Send(HttpMethod.Delete, $"sessions/{sessionId}", null, timeout.Token);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or HomeRunnerException)
        {
            // The worker independently expires unclaimed sessions even when its endpoint is unreachable.
        }
    }

    internal async Task KeepAlive(Guid sessionId, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var response = await Send(HttpMethod.Get, $"sessions/{sessionId}", null, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new HomeRunnerException("RUNNER_EXPIRED", "Kodearbejderens session er udløbet. Start et nyt forsøg.");
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, HttpContent? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method,
            new Uri(BaseUri ?? throw new HomeRunnerException("RUNNER_UNAVAILABLE",
                "Kodearbejderen er ikke konfigureret. Din kode og indretning er gemt."), path)) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.RunnerToken);
        var client = clients.CreateClient("home-game");
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new HomeRunnerException("RUNNER_BUSY", "Kodearbejderen er optaget. Prøv igen om lidt.");
        if (!response.IsSuccessStatusCode)
            throw new HomeRunnerException("RUNNER_UNAVAILABLE", "Forbindelsen til kodearbejderen blev afbrudt.");
        if (response.Content.Headers.ContentLength > MaxReplyBytes) throw InvalidReply();

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var data = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                var length = await stream.ReadAsync(buffer, ct);
                if (length == 0) break;
                if (data.Length + length > MaxReplyBytes) throw InvalidReply();
                data.Write(buffer, 0, length);
            }
            return JsonSerializer.Deserialize<T>(data.GetBuffer().AsSpan(0, (int)data.Length), HomeGameJson.Options)
                ?? throw InvalidReply();
        }
        catch (JsonException)
        {
            throw InvalidReply();
        }
    }

    private static HomeRunnerException Failure(HomeRunnerError error)
    {
        var code = error.Code is { Length: > 0 and <= 64 }
            && error.Code.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? error.Code : "RUNNER_ERROR";
        var message = error.Message ?? "Kodearbejderen afsluttede forsøget.";
        return new HomeRunnerException(code, message.Length <= 4096 ? message : message[..4096]);
    }

    private static HomeRunnerException InvalidReply() =>
        new("INVALID_BATCH", "Kodearbejderen sendte et ugyldigt svar. Forsøget blev standset.");

    private static StringContent Content<T>(T value) =>
        new(HomeGameJson.Write(value), Encoding.UTF8, "application/json");
}
