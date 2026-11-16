using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Web.HomeGame;

public sealed class HomeGameClient(HttpClient http)
{
    private const string Path = "me/home-game";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<HomeGameState> LoadAsync(string subject, CancellationToken cancellation) =>
        SendAsync<HomeGameState>(HttpMethod.Get, Path, subject, null, cancellation);

    public Task<HomeGameState> BuildAsync(string subject, HomeBuildRequest request, CancellationToken cancellation) =>
        SendAsync<HomeGameState>(HttpMethod.Post, $"{Path}/build", subject, request, cancellation);

    public Task<HomeGameState> SaveAsync(string subject, HomeSaveProgramRequest request, CancellationToken cancellation) =>
        SendAsync<HomeGameState>(HttpMethod.Put, $"{Path}/program", subject, request, cancellation);

    public Task<HomeGameState> ReflectAsync(string subject, HomeReflectionRequest request, CancellationToken cancellation) =>
        SendAsync<HomeGameState>(HttpMethod.Put, $"{Path}/reflection", subject, request, cancellation);

    public Task<HomeHelpEntry> HelpAsync(string subject, HomeHelpRequest request, CancellationToken cancellation) =>
        SendAsync<HomeHelpEntry>(HttpMethod.Post, $"{Path}/help", subject, request, cancellation);

    public Task<HomeRunStatus> StartAsync(string subject, HomeStartRunRequest request, CancellationToken cancellation) =>
        SendAsync<HomeRunStatus>(HttpMethod.Post, $"{Path}/runs", subject, request, cancellation);

    public Task<HomeRunStatus> RunAsync(string subject, Guid id, CancellationToken cancellation) =>
        SendAsync<HomeRunStatus>(HttpMethod.Get, $"{Path}/runs/{id}", subject, null, cancellation);

    public Task<HomeRunStatus> StepAsync(string subject, Guid id, HomeStepRunRequest request, CancellationToken cancellation) =>
        SendAsync<HomeRunStatus>(HttpMethod.Post, $"{Path}/runs/{id}/step", subject, request, cancellation);

    public Task<HomeRunStatus> ControlAsync(string subject, Guid id, HomeRunControlRequest request, CancellationToken cancellation) =>
        SendAsync<HomeRunStatus>(HttpMethod.Post, $"{Path}/runs/{id}/control", subject, request, cancellation);

    public Task<HomeRunReplay> ReplayAsync(string subject, Guid id, CancellationToken cancellation) =>
        SendAsync<HomeRunReplay>(HttpMethod.Get, $"{Path}/runs/{id}/replay", subject, null, cancellation);

    public Task<HomeTeacherOverview> ClassAsync(string subject, Guid id, CancellationToken cancellation) =>
        SendAsync<HomeTeacherOverview>(HttpMethod.Get, $"holds/{id}/home-game", subject, null, cancellation);

    public Task<HomeTeacherSettings> SettingsAsync(string subject, Guid id, HomeTeacherSettings request, CancellationToken cancellation) =>
        SendAsync<HomeTeacherSettings>(HttpMethod.Put, $"holds/{id}/home-game/settings", subject, request, cancellation);

    public Task<HomeTeacherStudentDetail> StudentAsync(string subject, Guid id, Guid student, CancellationToken cancellation,
        Guid? run = null) =>
        SendAsync<HomeTeacherStudentDetail>(HttpMethod.Get,
            $"holds/{id}/home-game/students/{student}{(run is Guid runId ? $"?runId={runId}" : "")}", subject, null, cancellation);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, string subject, object? payload,
        CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var request = new HttpRequestMessage(method, path);
        if (payload is not null)
            request.Content = JsonContent.Create(payload, payload.GetType(), options: JsonOptions);
        request.Headers.TryAddWithoutValidation("X-LUP-Apartment-Subject", subject);
        request.SetBrowserRequestCache(BrowserRequestCache.NoStore);

        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<T>(JsonOptions, timeout.Token)
                    ?? throw new HomeGameRequestException("Serveren sendte et tomt svar. Prøv igen.");

            var fallback = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Log ind igen for at åbne Hjem.exe.",
                HttpStatusCode.Forbidden => "Du har ikke adgang til denne handling.",
                HttpStatusCode.Conflict => "En nyere version er gemt. Hent den før du prøver igen.",
                HttpStatusCode.NotFound => "Hjem.exe er ikke åbnet her endnu, eller forsøget findes ikke.",
                _ => "Handlingen kunne ikke gennemføres. Prøv igen."
            };
            var message = fallback;
            string? code = null;
            try
            {
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                if (problem.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (problem.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                        message = detail.GetString() ?? fallback;
                    if (problem.RootElement.TryGetProperty("code", out var error) && error.ValueKind == JsonValueKind.String)
                        code = error.GetString();
                }
            }
            catch (JsonException) { }
            throw new HomeGameRequestException(message, response.StatusCode == HttpStatusCode.Conflict, code);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new HomeGameRequestException("Serveren svarede ikke i tide. Prøv igen.");
        }
        catch (HttpRequestException)
        {
            throw new HomeGameRequestException("Forbindelsen blev afbrudt. Prøv igen, når du har forbindelse.");
        }
        catch (JsonException)
        {
            throw new HomeGameRequestException("Serverens svar kunne ikke læses. Genindlæs Hjem.exe.");
        }
    }
}

public sealed class HomeGameRequestException(string message, bool conflict = false, string? code = null) : Exception(message)
{
    public bool Conflict { get; } = conflict;
    public string? Code { get; } = code;
}
