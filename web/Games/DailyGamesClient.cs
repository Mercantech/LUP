using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Games;

public sealed class DailyGamesClient(HttpClient http)
{
    public Task<DailyGameDay> GetDayAsync(string game, CancellationToken ct) =>
        GetAsync<DailyGameDay>(game, "daily", ct);

    public Task<DailyGameRound> GetRoundAsync(string game, CancellationToken ct) =>
        GetAsync<DailyGameRound>(game, "round", ct);

    public async Task<DailyGameRound> MoveAsync(string game, string action, DailyGameMove move, string subject, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path(game, action))
        {
            Content = JsonContent.Create(move)
        };
        request.Headers.Add("X-LUP-Daily-Subject", subject);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is not (HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity))
            response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DailyGameRound>(ct);
        if (result?.Error == "account_changed")
            throw new HttpRequestException("Kontoen blev skiftet.", null, HttpStatusCode.Unauthorized);
        return result ?? throw new HttpRequestException("Spillets svar mangler.");
    }

    private async Task<T> GetAsync<T>(string game, string action, CancellationToken ct) =>
        await http.GetFromJsonAsync<T>(Path(game, action), ct)
        ?? throw new HttpRequestException("Spillets svar mangler.");

    private static string Path(string game, string action) => $"games/daily/{game}/{action}";
}
