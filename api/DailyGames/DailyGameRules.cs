using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mercantec.Tech.Api.Wordle;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.DailyGames;

internal sealed record DailyPuzzleData(
    string Host, string Theme, string? Clue = null, string? Answer = null,
    string[]? ExtraHints = null, string? Explanation = null, string? Example = null,
    string[]? AcceptedAnswers = null, int[]? Tiles = null, int? Target = null);

internal sealed class DailyPlayState
{
    public List<string> Guesses { get; set; } = [];
    public List<Guid> Requests { get; set; } = [];
    public int HintCount { get; set; }
    public string? Expression { get; set; }
    public int? Value { get; set; }
}

internal static partial class DailyGameRules
{
    internal const int MaxWordGuesses = 3;
    internal static readonly DateOnly Start = new(2026, 11, 14);

    private static readonly string[][] NumberPatterns =
    [
        ["a", "*", "b", "+", "c", "*", "d", "-", "e", "+", "f"],
        ["a", "*", "(", "b", "+", "c", ")", "/", "d", "+", "e", "*", "f"],
        ["(", "a", "-", "b", ")", "*", "(", "c", "+", "d", ")", "-", "e", "+", "f"],
        ["(", "a", "+", "b", "*", "c", ")", "*", "d", "+", "e", "-", "f"],
        ["a", "*", "(", "b", "+", "c", "+", "d", ")", "+", "e", "*", "f"],
        ["a", "*", "b", "-", "(", "c", "+", "d", ")", "*", "e", "+", "f"],
        ["(", "a", "+", "b", "+", "c", ")", "*", "(", "d", "+", "e", ")", "-", "f"],
        ["a", "+", "(", "b", "+", "c", ")", "*", "d", "*", "e", "-", "f"]
    ];

    internal static bool IsGame(string game) => game is DailyGameIds.Word or DailyGameIds.Numbers;

    internal static DailyPuzzleData Create(string game, DateOnly date, byte[] key)
    {
        var dayNumber = date.DayNumber - Start.DayNumber;
        if (game == DailyGameIds.Word)
        {
            var word = DailyWordCatalog.Choose(dayNumber, key);
            return new(word.Host, WordleLexicon.Theme(word.Host), word.Clue, word.Answer,
                [word.Hint, word.FinalHint], word.Explanation, word.Example, word.AcceptedAnswers);
        }

        var seed = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"daily-numbers:{dayNumber}"));
        var random = new Random(BinaryPrimitives.ReadInt32LittleEndian(seed));
        var hosts = DailyGameHosts.Numbers;
        var host = hosts[dayNumber % hosts.Count];
        var theme = host == "bilbo" ? "Regneudtryk med Bilbo Backend" : "Tal og mønstre med Kamæleonen";
        int[] large = [25, 50, 75, 100];
        string[] indices = ["0", "1", "2", "3", "4", "5"];
        for (var attempt = 0; attempt < 100; attempt++)
        {
            int[] tiles = [large[random.Next(large.Length)], random.Next(2, 11), random.Next(1, 11),
                random.Next(1, 11), random.Next(1, 11), random.Next(1, 11)];
            random.Shuffle(tiles);
            var pattern = NumberPatterns[random.Next(NumberPatterns.Length)];
            random.Shuffle(indices);
            var tokens = pattern.Select(token => token[0] is >= 'a' and <= 'f' ? indices[token[0] - 'a'] : token)
                .ToArray();
            if (DailyNumberExpression.TryEvaluate(tiles, tokens, out var target, out var example) && target is >= 100 and <= 999)
                return new(host, theme, Example: example, Tiles: tiles, Target: target);
        }

        int[] fallbackTiles = [100, 9, 7, 5, 3, 2];
        random.Shuffle(fallbackTiles);
        return new(host, theme, Example: "100 + 9", Tiles: fallbackTiles, Target: 109);
    }

    internal static string? NormalizeWord(string? value)
    {
        if (value is null || value.Length > 64) return null;
        string word;
        try { word = value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant(); }
        catch (ArgumentException) { return null; }
        return word.Length is >= 2 and <= 32 && DanishWord().IsMatch(word) ? word : null;
    }

    internal static string? WordPattern(string? answer, int hints)
    {
        if (answer is null) return null;

        var letters = answer.Where(char.IsLetter).Distinct().ToArray();
        // Always keep at least one distinct letter hidden, including in short words.
        var revealCount = Math.Clamp(hints, 0, Math.Max(0, letters.Length - 1));
        var revealed = letters.Take(revealCount).ToHashSet();
        return new string(answer.Select(letter => char.IsLetter(letter) && !revealed.Contains(letter) ? '_' : letter)
            .ToArray());
    }

    [GeneratedRegex(@"\A[a-zæøå]+(?:[ -][a-zæøå]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex DanishWord();
}
