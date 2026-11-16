using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.KnowledgeChecks;

public sealed class LiveKnowledgeCheckClient(HttpClient http)
{
    public Task<LiveQuizState> CreateAsync(LiveQuizCreate request, CancellationToken ct = default) =>
        SendAsync<LiveQuizState>(HttpMethod.Post, "", request, null, ct);

    public Task<LiveQuizState> GetAsync(string code, string? playerToken = null, CancellationToken cancellationToken = default) =>
        SendAsync<LiveQuizState>(HttpMethod.Get, code, null, playerToken, cancellationToken);

    public Task<LiveQuizJoined> JoinAsync(string code, LiveQuizJoin request, CancellationToken ct = default) =>
        SendAsync<LiveQuizJoined>(HttpMethod.Post, $"{code}/join", request, null, ct);

    public Task<LiveQuizState> AnswerAsync(string code, string playerToken, LiveQuizAnswer request, CancellationToken ct = default) =>
        SendAsync<LiveQuizState>(HttpMethod.Post, $"{code}/answer", request, playerToken, ct);

    public Task<LiveQuizState> AdvanceAsync(string code, LiveQuizAdvance request, CancellationToken ct = default) =>
        SendAsync<LiveQuizState>(HttpMethod.Post, $"{code}/advance", request, null, ct);

    public Task<LiveQuizState> EndAsync(string code, CancellationToken ct = default) =>
        SendAsync<LiveQuizState>(HttpMethod.Post, $"{code}/end", null, null, ct);

    public Task<LiveQuizState> KickAsync(string code, LiveQuizKick request, CancellationToken ct = default) =>
        SendAsync<LiveQuizState>(HttpMethod.Post, $"{code}/kick", request, null, ct);

    public Task<LiveQuizState> LockAsync(string code, LiveQuizLock request, CancellationToken ct = default) =>
        SendAsync<LiveQuizState>(HttpMethod.Post, $"{code}/lock", request, null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, string? playerToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path.Length == 0 ? "knowledge-checks/live" : $"knowledge-checks/live/{path}");
        if (body is not null) request.Content = JsonContent.Create(body);
        if (!string.IsNullOrEmpty(playerToken)) request.Headers.Add(LiveQuiz.PlayerHeader, playerToken);
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
                ?? throw new LiveQuizException(response.StatusCode, "empty_response");

        string error = "";
        try
        {
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            if (json?.RootElement is { ValueKind: JsonValueKind.Object } root
                && root.TryGetProperty("error", out var code) && code.ValueKind == JsonValueKind.String)
                error = code.GetString() ?? "";
        }
        catch (JsonException) { }
        throw new LiveQuizException(response.StatusCode, error);
    }
}

public sealed class LiveQuizException(HttpStatusCode status, string code) : Exception(MessageFor(status, code))
{
    public HttpStatusCode Status { get; } = status;
    public string Code { get; } = code;

    private static string MessageFor(HttpStatusCode status, string code) => code switch
    {
        "live_invalid_nickname" => "Skriv et kaldenavn på højst 24 tegn med mindst ét bogstav eller tal.",
        "live_nickname_taken" => "Navnet er i brug. Vælg et andet kaldenavn.",
        "live_locked" => "Værten har lukket for nye spillere.",
        "live_full" => "Spillet er fyldt.",
        "live_finished" => "Spillet er afsluttet.",
        "live_question_closed" => "Dette spørgsmål er lukket.",
        "live_already_answered" => "Dit svar er allerede gemt.",
        "live_invalid_answer" => "Vælg et gyldigt svar.",
        "live_no_players" => "Vent, til mindst én spiller er med.",
        "live_quiz_not_ready" => "Videnstjekket skal have færdige spørgsmål med automatisk facit.",
        "live_invalid_settings" => "Vælg et videnstjek og en gyldig tid pr. spørgsmål.",
        "live_source_not_found" => "Videnstjekket kunne ikke åbnes som klassespil.",
        "live_not_found" => "Spillet kunne ikke findes. Kontrollér PIN-koden.",
        "live_stale_state" => "Spillet har skiftet fase. Prøv igen.",
        _ => status switch
        {
            HttpStatusCode.NotFound => "Spillet eller videnstjekket kunne ikke findes.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Du har ikke adgang til dette spil.",
            HttpStatusCode.TooManyRequests => "Vent et øjeblik, og prøv igen.",
            _ => "LUP kunne ikke gennemføre handlingen. Prøv igen."
        }
    };
}
