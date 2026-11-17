namespace Mercantec.Tech.Api.Learning;

internal static class PollRules
{
    public static string[] Options(string kind, string[]? options) => kind switch
    {
        "mood" => ["Glad", "Klar", "Midt imellem", "Træt", "Presset"],
        "scale" => ["1", "2", "3", "4", "5"],
        "text" => [],
        _ => (options ?? []).Select(option => option?.Trim() ?? "").ToArray()
    };

    public static bool ValidChoices(string[] options) => options.Length is >= 2 and <= 6
        && options.All(s => s.Length is >= 1 and <= 100)
        && options.Distinct(StringComparer.OrdinalIgnoreCase).Count() == options.Length;

    public static bool ValidAnswer(string kind, int optionCount, int? choice, string? text) =>
        kind == "text" ? !string.IsNullOrWhiteSpace(text) && text.Length <= 280 && choice is null
            : choice is int index && index >= 0 && index < optionCount && string.IsNullOrEmpty(text);

    public static int[] Counts(int optionCount, IEnumerable<int?> choices)
    {
        var counts = new int[optionCount];
        foreach (var choice in choices)
            if (choice is int index && index >= 0 && index < counts.Length) counts[index]++;
        return counts;
    }
}
