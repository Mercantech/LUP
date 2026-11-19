using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Components.Forms;

namespace Mercantec.Tech.Web.Students;

public sealed class ProfilePhotoClient(HttpClient http)
{
    public async Task<ProfilePhotoView> Mine(CancellationToken ct)
    {
        using var response = await http.GetAsync("me/profile-photo", ct);
        return await Read(response, ct);
    }

    public async Task<string?> Photo(Guid sub, CancellationToken ct)
    {
        return await ReadPhoto($"student-photos/{sub:D}", ct);
    }

    public Task<string?> LectioPhoto(string lectioId, CancellationToken ct) =>
        ReadPhoto($"lectio/students/{Uri.EscapeDataString(lectioId)}/photo", ct);

    public async Task<string?> OwnLectioId(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await http.GetAsync("lectio/me", timeout.Token);
            if (!response.IsSuccessStatusCode) return null;
            var me = await response.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
            return me.ValueKind == JsonValueKind.Object && me.TryGetProperty("lectio", out var lectio)
                && lectio.ValueKind == JsonValueKind.Object && lectio.TryGetProperty("lectioId", out var id)
                && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    private async Task<string?> ReadPhoto(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var type = response.Content.Headers.ContentType?.MediaType;
        if (type != "image/jpeg") return null;
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
    }

    public async Task<ProfilePhotoView> Upload(IBrowserFile file, string expectedSubject, CancellationToken ct)
    {
        if (file.Size > ProfilePhotoRules.MaxBytes) throw new ProfilePhotoException("Billedet må højst fylde 2 MB.");
        await using var stream = file.OpenReadStream(ProfilePhotoRules.MaxBytes, ct);
        using var multipart = new MultipartFormDataContent();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(content, "file", file.Name);
        using var request = new HttpRequestMessage(HttpMethod.Post, "me/profile-photo") { Content = multipart };
        request.Headers.Add(ProfileNameRules.SubjectHeader, expectedSubject);
        using var response = await http.SendAsync(request, ct);
        return await Read(response, ct);
    }

    public async Task<ProfilePhotoView> Delete(string expectedSubject, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "me/profile-photo");
        request.Headers.Add(ProfileNameRules.SubjectHeader, expectedSubject);
        using var response = await http.SendAsync(request, ct);
        return await Read(response, ct);
    }

    private static async Task<ProfilePhotoView> Read(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            PhotoError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<PhotoError>(ct); }
            catch (JsonException) { }
            catch (NotSupportedException) { }
            throw new ProfilePhotoException(error?.Error switch
            {
                "invalid_photo" => "Vælg et gyldigt JPG-, PNG- eller WebP-billede på højst 20 megapixel.",
                "file_too_large" => "Billedet må højst fylde 2 MB.",
                "photo_changed" => "Billedet er ændret i en anden fane. Hent siden igen, og prøv igen.",
                "account_changed" => "Den aktive konto er ændret. Hent siden igen, og vælg billedet på ny.",
                "object_storage_disabled" => "Billedlageret er ikke tilgængeligt lige nu. Prøv igen senere.",
                _ => "Billedet kunne ikke gemmes. Prøv igen."
            });
        }
        try
        {
            return await response.Content.ReadFromJsonAsync<ProfilePhotoView>(ct)
                ?? throw new ProfilePhotoException("Kunne ikke hente dit profilfoto.");
        }
        catch (JsonException) { throw new ProfilePhotoException("Kunne ikke hente dit profilfoto. Prøv igen."); }
    }

    private sealed record PhotoError(string? Error);
}

public sealed class ProfilePhotoException(string message) : Exception(message);
