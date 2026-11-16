using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.KnowledgeChecks;

internal static class LegacyKnowledgeCheckCatalog
{
    private static readonly Lazy<Template[]> Templates = new(Load);

    internal static IReadOnlyDictionary<Guid, Template> ForOwner(Guid owner) =>
        Templates.Value.ToDictionary(template => DraftId(owner, template.Key));

    private static Guid DraftId(Guid owner, string key)
    {
        // Keep imported drafts discoverable after their titles are edited.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"lup:videnstjek:legacy:v1:{owner:N}:{key}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static Template[] Load()
    {
        using var source = typeof(LegacyKnowledgeCheckCatalog).Assembly.GetManifestResourceStream("Mercantec.Tech.Api.LegacyKnowledgeChecks.json")
            ?? throw new InvalidDataException("Missing embedded legacy knowledge checks.");
        var bundle = JsonSerializer.Deserialize<Bundle>(source, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (bundle?.Drafts is not { Length: > 0 } drafts) throw new InvalidDataException("No legacy knowledge checks.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        return drafts.Select(template =>
        {
            if (string.IsNullOrWhiteSpace(template.Key) || !keys.Add(template.Key)
                || template.Draft is null || !NormalizeDraft(template.Draft, out var draft, out _)
                || draft.Questions.Length == 0 || draft.Questions.Any(question => !IsReady(question)))
                throw new InvalidDataException($"Invalid embedded legacy knowledge check '{template.Key}'.");

            return template with { Draft = draft };
        }).ToArray();
    }

    private sealed record Bundle(Template[] Drafts);
    internal sealed record Template(string Key, KnowledgeCheckDraft Draft, KnowledgeCheckLegacyDiscovery? LegacyDiscovery)
    {
        internal KnowledgeCheckLegacyDiscovery? DiscoveryForTopic(string topic) =>
            LegacyDiscovery is { } discovery && !discovery.Categories.Contains(topic, StringComparer.OrdinalIgnoreCase)
                ? discovery with { Categories = string.IsNullOrWhiteSpace(topic) ? [] : [topic] }
                : LegacyDiscovery;
    }
}
