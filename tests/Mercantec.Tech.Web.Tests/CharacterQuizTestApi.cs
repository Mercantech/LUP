using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Characters;

namespace Mercantec.Tech.Web.Tests;

internal sealed class CharacterQuizTestApi(
    string? characterSlug = null, KnowledgeCheckCharacterSummary[]? quizzes = null) : HttpMessageHandler
{
    private const string Prefix = "/api/knowledge-checks/characters/";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = request.RequestUri?.AbsolutePath;
        if (request.Method != HttpMethod.Get || path is null || !path.StartsWith(Prefix, StringComparison.Ordinal)
            || CharacterCatalog.Find(path[Prefix.Length..]) is null)
            throw new InvalidOperationException($"Unexpected character profile request: {request.Method} {request.RequestUri}");

        KnowledgeCheckCharacterSummary[] items = path == Prefix + characterSlug ? quizzes ?? [] : [];
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(items) });
    }
}
