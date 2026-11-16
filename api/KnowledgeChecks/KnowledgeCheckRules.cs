using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.KnowledgeChecks;

internal static class KnowledgeCheckRules
{
    public const string Started = "started", Submitted = "submitted", Graded = "graded";
    public const string AfterSubmit = "after-submit", Manual = "manual";
    public const string Single = "single", Multiple = "multiple", Boolean = "boolean", Short = "short";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly char[] UnsafeLinkCharacters = ['\\', '\r', '\n', '\t'];

    public static KnowledgeCheckDraft? Pilot()
    {
        using var stream = typeof(KnowledgeCheckRules).Assembly.GetManifestResourceStream("Mercantec.Tech.Api.ControlFlowPilot.json");
        return stream is null ? null : JsonSerializer.Deserialize<KnowledgeCheckDraft>(stream, Json);
    }

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Json);

    public static T[] Read<T>(string value) => JsonSerializer.Deserialize<T[]>(value, Json) ?? [];

    public static bool NormalizeDraft(KnowledgeCheckDraft input, out KnowledgeCheckDraft value, out int? invalidQuestion)
    {
        value = input with { Title = Trim(input.Title), Topic = Trim(input.Topic), Description = Trim(input.Description) };
        invalidQuestion = null;
        if (value.Title.Length is < 1 or > 160 || value.Description.Length > 1000 || value.Topic.Length > 80
            || value.EstimatedMinutes is < 1 or > 240 || value.Questions is null || value.Questions.Length > 60) return false;

        var questions = new KnowledgeCheckQuestion[value.Questions.Length];
        var ids = new HashSet<Guid>();
        for (var index = 0; index < questions.Length; index++)
        {
            var question = Normalize(value.Questions[index]);
            if (question is null || !ids.Add(question.Id))
            {
                invalidQuestion = index;
                return false;
            }
            questions[index] = question;
        }
        value = value with { Questions = questions };
        return true;
    }

    private static KnowledgeCheckQuestion? Normalize(KnowledgeCheckQuestion? question)
    {
        if (question is null || question.Id == Guid.Empty || question.Kind is not (Single or Multiple or Boolean or Short)) return null;

        var options = question.Kind switch
        {
            Short => [],
            Boolean => ["Sandt", "Falsk"],
            _ => (question.Options ?? []).Select(Trim).ToArray()
        };
        var accepted = question.Kind == Short && !question.ManualGrading
            ? (question.AcceptedAnswers ?? []).Select(Trim).Where(x => x.Length > 0).ToArray() : [];
        var correct = question.Kind == Short ? [] : (question.CorrectOptions ?? []).Distinct().Order().ToArray();
        var prompt = Trim(question.Prompt);
        var explanation = Trim(question.Explanation);
        var url = Trim(question.LearnMoreUrl);
        var optionExplanations = question.Kind == Short ? null : question.OptionExplanations?.Select(Trim).ToArray();
        var sourceTitle = url.Length > 0 ? Trim(question.LearnMoreTitle) : "";
        var sourceDescription = url.Length > 0 ? Trim(question.LearnMoreDescription) : "";
        if (prompt.Length > 2400 || explanation.Length > 2400 || !IsSafeLink(url) || options.Length > 8
            || options.Any(option => option.Length > 500) || correct.Any(index => index < 0 || index >= options.Length)
            || accepted.Length > 20 || accepted.Any(option => option.Length > 250)
            || optionExplanations is { Length: > 0 } && (optionExplanations.Length != options.Length
                || optionExplanations.Any(text => text.Length > 2400))
            || sourceTitle.Length > 200 || sourceDescription.Length > 1000) return null;

        return question with
        {
            Prompt = prompt, Options = options, CorrectOptions = correct, AcceptedAnswers = accepted,
            ManualGrading = question.Kind == Short && question.ManualGrading,
            Explanation = explanation, LearnMoreUrl = url.Length == 0 ? null : url,
            OptionExplanations = optionExplanations?.Any(text => text.Length > 0) == true ? optionExplanations : null,
            LearnMoreTitle = sourceTitle.Length == 0 ? null : sourceTitle,
            LearnMoreDescription = sourceDescription.Length == 0 ? null : sourceDescription
        };
    }

    public static bool IsReady(KnowledgeCheckQuestion question)
    {
        if (question.Prompt.Length == 0) return false;
        if (question.Kind == Short) return question.ManualGrading || question.AcceptedAnswers.Length > 0;
        return question.Options.Length >= 2 && question.Options.All(option => option.Length > 0)
            && question.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() == question.Options.Length
            && (question.Kind == Multiple ? question.CorrectOptions.Length >= 2 : question.CorrectOptions.Length == 1);
    }

    public static KnowledgeCheckAnswer[]? NormalizeAnswers(KnowledgeCheckQuestion[] questions, KnowledgeCheckSave request)
    {
        if (request.Answers is null || request.QuestionIndex < 0 || request.QuestionIndex >= questions.Length
            || request.Answers.Length > questions.Length) return null;
        var lookup = questions.ToDictionary(question => question.Id);
        var seen = new HashSet<Guid>();
        var result = new List<KnowledgeCheckAnswer>();
        foreach (var answer in request.Answers)
        {
            if (answer is null || !lookup.TryGetValue(answer.QuestionId, out var question) || !seen.Add(answer.QuestionId)) return null;
            if (!TryNormalizeAnswer(question, answer, out var normalized)) return null;
            if (normalized is not null) result.Add(normalized);
        }
        return result.ToArray();
    }

    public static bool TryNormalizeAnswer(KnowledgeCheckQuestion question, KnowledgeCheckAnswer answer,
        out KnowledgeCheckAnswer? normalized)
    {
        normalized = null;
        var choices = answer.SelectedOptions ?? [];
        var text = Trim(answer.Text);
        if (question.Kind == Short)
        {
            if (text.Length > 1000 || choices.Length != 0) return false;
            if (text.Length > 0) normalized = new(answer.QuestionId, [], text);
            return true;
        }
        if (text.Length != 0 || choices.Length > question.Options.Length || choices.Length != choices.Distinct().Count()
            || choices.Any(index => index < 0 || index >= question.Options.Length)
            || question.Kind != Multiple && choices.Length > 1) return false;
        if (choices.Length > 0) normalized = new(answer.QuestionId, choices.Order().ToArray(), null);
        return true;
    }

    public static StoredGrade Grade(KnowledgeCheckQuestion question, KnowledgeCheckAnswer answer)
    {
        bool? correct = question.Kind switch
        {
            Short when question.ManualGrading => null,
            Short => question.AcceptedAnswers.Any(value => NormalizeText(value)
                .Equals(NormalizeText(answer.Text!), StringComparison.OrdinalIgnoreCase)),
            _ => question.CorrectOptions.SequenceEqual(answer.SelectedOptions)
        };
        return new(question.Id, correct, null);
    }

    public static void SetGrades(KnowledgeCheckAttempt attempt, StoredGrade[] grades)
    {
        attempt.GradesJson = Write(grades);
        var complete = grades.All(grade => grade.Correct is not null);
        attempt.Status = complete ? Graded : Submitted;
        attempt.Score = complete ? grades.Count(grade => grade.Correct == true) : null;
    }

    public static bool CanSeeFeedback(KnowledgeCheckPublication publication) =>
        publication.FeedbackMode == AfterSubmit || publication.FeedbackReleased;

    private static string NormalizeText(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormC);

    private static string Trim(string? value) => value?.Trim() ?? "";

    private static bool IsSafeLink(string url) => url.Length == 0 || url.Length <= 1500
        && !url.Any(char.IsControl) && url.IndexOfAny(UnsafeLinkCharacters) < 0
        && (url[0] == '/' && !url.StartsWith("//", StringComparison.Ordinal)
            || Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps
                && parsed.Host.Length > 0 && parsed.UserInfo.Length == 0);

    internal sealed record StoredGrade(Guid QuestionId, bool? Correct, string? Comment);
}
