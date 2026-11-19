using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Students;

public sealed class StudentDirectoryClient(HttpClient http)
{
    public async Task<StudentDirectoryResponse> GetAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("students", ct);
        return await Read<StudentDirectoryResponse>(response, ct);
    }

    public async Task<StudentDirectoryStudent> UpdateAsync(string key, UpdateStudentDirectoryDetails details,
        string expectedSubject, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"students/{Uri.EscapeDataString(key)}/details")
        {
            Content = JsonContent.Create(details)
        };
        request.Headers.Add(ProfileNameRules.SubjectHeader, expectedSubject);
        using var response = await http.SendAsync(request, ct);
        return await Read<StudentDirectoryStudent>(response, ct);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = (int)response.StatusCode switch
            {
                400 => "Kontrollér felterne og længden på teksterne.",
                401 => "Log ind igen for at se og rette elevoplysninger.",
                403 => "Din konto har ikke adgang til elevoversigten.",
                404 => "Eleven er ikke længere tilgængelig. Hent elevlisten igen.",
                409 => "Den aktive konto er ændret. Hent elevlisten igen, før du gemmer.",
                _ => "Elevoplysningerne kunne ikke hentes eller gemmes. Prøv igen."
            };
            throw new StudentDirectoryException(message);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct)
                ?? throw new StudentDirectoryException("Serveren returnerede et tomt svar. Prøv igen.");
        }
        catch (JsonException)
        {
            throw new StudentDirectoryException("Serveren returnerede et uventet svar. Prøv igen.");
        }
    }
}

public sealed class StudentDirectoryException(string message) : Exception(message);
