using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Microsoft.AspNetCore.Components.Forms;

namespace Mercantec.Tech.Web.Notebooks;

public sealed class NotebookCoverClient(HttpClient http, AuthService auth)
{
    public async Task<byte[]?> ReadAsync(string kind, Guid id, Guid version, bool publishedOnly, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get,
            $"{Path(kind, id)}?version={version:D}" + (publishedOnly ? "&publishedOnly=true" : ""));
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType != "image/webp") return null;
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<NotebookCoverView> UploadAsync(string kind, Guid id, IBrowserFile file, CancellationToken ct)
    {
        if (file.Size > NotebookCoverRules.MaxUploadBytes)
            throw new NotebookCoverException("Vælg et billede på højst 5 MB.");
        using var request = CreateRequest(HttpMethod.Put, Path(kind, id));
        await using var stream = file.OpenReadStream(NotebookCoverRules.MaxUploadBytes, ct);
        using var image = new MemoryStream((int)file.Size);
        await stream.CopyToAsync(image, ct);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(image.ToArray()), "file", file.Name);
        request.Content = form;
        return await MutateAsync(request, ct);
    }

    public async Task<NotebookCoverView> GenerateAsync(string kind, Guid id, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Post, Path(kind, id) + "/generate");
        return await MutateAsync(request, ct);
    }

    public async Task<NotebookCoverView> DeleteAsync(string kind, Guid id, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Delete, Path(kind, id));
        return await MutateAsync(request, ct);
    }

    private async Task<NotebookCoverView> MutateAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<NotebookCoverView>(ct)
                ?? throw new NotebookCoverException("Coveret kunne ikke hentes efter ændringen.");

        string? error = null;
        try { error = (await response.Content.ReadFromJsonAsync<CoverError>(ct))?.Error; }
        catch (Exception ex) when (ex is JsonException or NotSupportedException) { }

        throw new NotebookCoverException(error switch
        {
            "invalid_cover_image" => "Vælg et gyldigt billede i JPG, PNG eller WebP.",
            "cover_too_large" => "Vælg et billede på højst 5 MB.",
            "cover_changed" => "Coveret er ændret i en anden fane. Genindlæs siden, og prøv igen.",
            "account_changed" => "Den aktive konto er ændret. Genindlæs siden, og prøv igen.",
            "cover_generation_unavailable" => "AI-covere er ikke tilgængelige lige nu. Du kan stadig uploade et billede.",
            "cover_generation_failed" => "AI kunne ikke generere et cover. Prøv igen om lidt.",
            _ when response.StatusCode == HttpStatusCode.TooManyRequests => "Der er nået en grænse for AI-covere. Prøv igen senere.",
            _ when response.StatusCode == HttpStatusCode.RequestEntityTooLarge => "Vælg et billede på højst 5 MB.",
            _ => "Coveret kunne ikke ændres. Prøv igen."
        });
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (Guid.TryParse(auth.Subject, out var subject))
            request.Headers.Add(NotebookCoverRules.SubjectHeader, subject.ToString("D"));
        return request;
    }

    private static string Path(string kind, Guid id) => $"notebook-covers/{Uri.EscapeDataString(kind)}/{id:D}";
    private sealed record CoverError(string? Error);
}

public sealed class NotebookCoverException(string message) : Exception(message);
