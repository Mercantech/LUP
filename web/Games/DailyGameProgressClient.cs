using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Games;

public sealed class DailyGameProgressClient(HttpClient http)
{
    private const string Path = "games/progress";

    public Task<DailyHistory> HistoryAsync(string game, int page, CancellationToken ct) =>
        GetAsync<DailyHistory>($"{Path}/{game}/history?page={page}", ct);

    public Task<DailyLeaderboard> LeaderboardAsync(string game, string period, Guid? holdId, CancellationToken ct) =>
        GetAsync<DailyLeaderboard>($"{Path}/{game}/leaderboard?period={period}"
            + (holdId is Guid hold ? $"&holdId={hold:D}" : ""), ct);

    public Task<DailyRankingSettings> SettingsAsync(CancellationToken ct) =>
        GetAsync<DailyRankingSettings>($"{Path}/settings", ct);

    public async Task<DailyRankingSettings> SetVisibilityAsync(bool visible, string subject, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Path}/settings")
        {
            Content = JsonContent.Create(new ChangeDailyRankingVisibility(visible))
        };
        request.Headers.Add("X-LUP-Profile-Subject", subject);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DailyRankingSettings>(ct)
            ?? throw new HttpRequestException("Ranglisteindstillingerne mangler.");
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct) =>
        await http.GetFromJsonAsync<T>(path, ct)
        ?? throw new HttpRequestException("Spillets fremskridt mangler.");
}
