using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mercantec.Tech.Web.KnowledgeChecks;

public sealed class KnowledgeCheckClient(HttpClient http)
{
    public Task<T> GetAsync<T>(string path = "") where T : class => SendAsync<T>(HttpMethod.Get, path);

    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null) where T : class
    {
        using var response = await SendRequestAsync(method, path, body);
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new KnowledgeCheckException("LUP modtog et tomt svar.");
    }

    public async Task SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var response = await SendRequestAsync(method, path, body);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path.Length == 0 ? "knowledge-checks" : $"knowledge-checks/{path}");
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode) return response;

        using (response)
        {
            var status = response.StatusCode;
            try
            {
                using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
                if (json?.RootElement.ValueKind == JsonValueKind.Object
                    && json.RootElement.TryGetProperty("error", out var error))
                {
                    var message = error.GetString() switch
                    {
                        "invalid_draft" => "Kontrollér titel, varighed og spørgsmål.",
                        "invalid_question" => InvalidQuestion(json.RootElement),
                        "invalid_publication" => "Kontrollér målgruppe, forsøgsgrænse og tidspunkt for facit.",
                        "manual_grading_requires_hold" => "Vælg et hold, når et kortsvar skal vurderes manuelt.",
                        "draft_changed" => "Kladdeændringer fra en anden fane er gemt. Genindlæs siden for at se dem.",
                        "publication_closed" => "Udgivelsen er lukket.",
                        _ => null
                    };
                    if (message is not null) throw new KnowledgeCheckException(message);
                }
            }
            catch (JsonException) { }

            throw new KnowledgeCheckException(status switch
            {
                HttpStatusCode.Unauthorized => "Log ind igen for at fortsætte.",
                HttpStatusCode.Forbidden or HttpStatusCode.NotFound => "Du har ikke adgang, eller videnstjekket er fjernet.",
                _ => "LUP kunne ikke gennemføre handlingen. Prøv igen."
            });
        }
    }

    private static string InvalidQuestion(JsonElement error) =>
        error.TryGetProperty("questionIndex", out var index) && index.TryGetInt32(out var ordinal)
            ? $"Kontrollér spørgsmål {ordinal + 1}, svar og facit."
            : "Kontrollér spørgsmålene, svarene og facit.";
}

public sealed class KnowledgeCheckException(string message) : Exception(message);
