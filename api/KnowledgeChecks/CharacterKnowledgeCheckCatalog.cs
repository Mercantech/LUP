using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Characters;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.KnowledgeChecks;

internal static class CharacterKnowledgeCheckCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<Guid, Template>> Templates = new(Load);

    internal static IReadOnlyDictionary<Guid, Template> All => Templates.Value;

    internal static KnowledgeCheckCharacterHost? Host(Guid quizId) =>
        All.TryGetValue(quizId, out var template)
            ? new(template.CharacterSlug, template.Intro, template.Difficulty)
            : null;

    internal static Guid[] QuizIds(string slug) =>
        All.Where(item => item.Value.CharacterSlug == slug).Select(item => item.Key).ToArray();

    private static Guid DraftId(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"lup:videnstjek:character:v1:{key}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static IReadOnlyDictionary<Guid, Template> Load()
    {
        using var source = typeof(CharacterKnowledgeCheckCatalog).Assembly
            .GetManifestResourceStream("Mercantec.Tech.Api.CharacterKnowledgeChecks.json")
            ?? throw new InvalidDataException("Missing embedded character knowledge checks.");
        var bundle = JsonSerializer.Deserialize<Bundle>(source, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (bundle?.Drafts is not { Length: > 0 } drafts) throw new InvalidDataException("No character knowledge checks.");

        var result = new Dictionary<Guid, Template>();
        var characters = CharacterCatalog.All.Select(character => character.Slug).ToHashSet(StringComparer.Ordinal);
        foreach (var template in drafts)
        {
            if (string.IsNullOrWhiteSpace(template.Key) || !characters.Contains(template.CharacterSlug)
                || string.IsNullOrWhiteSpace(template.Intro) || template.Intro.Length > 360
                || template.Difficulty is not ("Begynder" or "Mellem" or "Avanceret")
                || template.Draft is null || !NormalizeDraft(template.Draft, out var draft, out _)
                || draft.Questions.Length != 6
                || draft.Questions.Any(question => !IsReady(question) || question.ManualGrading
                    || string.IsNullOrWhiteSpace(question.Explanation))
                || !result.TryAdd(DraftId(template.Key), template with { Draft = draft }))
                throw new InvalidDataException($"Invalid embedded character knowledge check '{template.Key}'.");
        }
        return result;
    }

    private sealed record Bundle(Template[] Drafts);
    internal sealed record Template(string Key, string CharacterSlug, string Intro, string Difficulty, KnowledgeCheckDraft Draft);
}
