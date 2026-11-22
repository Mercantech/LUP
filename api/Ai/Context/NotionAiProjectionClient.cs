using System.Net.Http.Json;
using Mercantec.Tech.Api.Notebooks;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

/// <summary>ASP.NET authorizes the source before handing only its selected bytes to the existing Yjs engine.</summary>
public sealed class NotionAiProjectionClient(IHttpClientFactory clients, IConfiguration configuration, NotebookServiceKey key)
{
    public Task<NotionAiProjection> ProjectAsync(byte[] state, CancellationToken ct) =>
        ProjectAsync<NotionAiProjection>(state, null, ct);

    internal Task<NotebookCoverProjection> ProjectCoverAsync(byte[] state, CancellationToken ct) =>
        ProjectAsync<NotebookCoverProjection>(state, "notebook-cover", ct);

    internal Task<LiveCourseProjection> ProjectLiveAsync(byte[] state, CancellationToken ct) =>
        ProjectAsync<LiveCourseProjection>(state, "live-presentation", ct);

    private async Task<T> ProjectAsync<T>(byte[] state, string? mode, CancellationToken ct) where T : class
    {
        if (state.Length > 16 * 1024 * 1024) throw new InvalidDataException("document_too_large");
        var secret = key.Read() ?? throw new HttpRequestException("Document service is not configured.");
        var origin = configuration["Notebook:CollaborationBaseUrl"] ?? "http://mercantec-tech-collaboration:1234";
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(origin), "/internal/notion/project"));
        message.Headers.Add("X-Notion-Service-Key", secret);
        message.Content = JsonContent.Create(new { stateBase64 = Convert.ToBase64String(state), mode });
        using var response = await clients.CreateClient("ai-projection").SendAsync(message, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new HttpRequestException("Invalid document projection.");
    }
}
public sealed record NotionAiProjection(string PlainText, NotionAiProjectedBlock[]? Blocks = null);
public sealed record NotionAiProjectedBlock(string? Id, string Type, string Text, string? Heading);
internal sealed record NotebookCoverProjection(string PlainText, NotebookCoverEmoji[]? Emojis = null);
internal sealed record LiveCourseProjection(int SlideCount, LiveCourseBinding[] Bindings);
internal sealed record LiveCourseBinding(string BlockId, Guid ModuleId, int SlideIndex);
