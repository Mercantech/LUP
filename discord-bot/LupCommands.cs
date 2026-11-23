using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;

namespace Lup.DiscordBot;

public sealed class LupCommands(HttpClient http)
{
    public Task<string> ReplyAsync(string command, string? topic, CancellationToken cancellationToken)
    {
        return command switch
        {
            "søg" => Task.FromResult(Search(topic?.Trim())),
            "spil" => Task.FromResult(JoinGame(topic?.Trim())),
            "taljagt" => DailyNumbersAsync(cancellationToken),
            _ => Task.FromResult(
                "**LUP i Discord**\n" +
                "`/lup søg` åbner en søgning på LUP. Prøv fx C# eller Git.\n" +
                "`/lup spil` åbner klassens spil med den sekscifrede kode fra din lærer.\n" +
                "`/lup taljagt` viser dagens seks tal. Hvor tæt kan du komme på målet?\n" +
                Link("Åbn LUP’s oversigt", "/library"))
        };
    }

    private string Search(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic) || topic.Length > 200)
            return "Skriv et søgeord på mellem 1 og 200 tegn, fx Git.";

        return Link("Se søgeresultater på LUP", "/search?q=" + Uri.EscapeDataString(topic));
    }

    private string JoinGame(string? code) => LiveQuiz.IsCode(code)
        ? Link("Deltag i klassens spil", "/spil/" + code)
        : "Skriv de seks cifre fra lærerens spilkode, også hvis den begynder med 0.";

    private async Task<string> DailyNumbersAsync(CancellationToken cancellationToken)
    {
        var play = Link("Spil Taljagt på LUP", "/games/" + DailyGameIds.Numbers);
        try
        {
            var day = await http.GetFromJsonAsync<DailyGameDay>(
                $"api/games/daily/{DailyGameIds.Numbers}/daily", cancellationToken);
            if (day is { Date: var date, Tiles: { Count: 6 } tiles, Target: int target }
                && date != default && target > 0 && tiles.All(number => number > 0))
            {
                return $"**Taljagt · {date:dd.MM.yyyy}**\n" +
                    $"Kom så tæt på **{target}** som muligt med **{string.Join(" · ", tiles)}**.\n" +
                    "Brug mindst to brikker og hver brik højst én gang. Du må bruge +, −, × og ÷.\n" + play;
            }
        }
        catch (HttpRequestException) { }
        catch (JsonException) { }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }

        return "Jeg kunne ikke hente dagens tal lige nu. " + play;
    }

    private string Link(string label, string path) =>
        $"[{label}](<{new Uri(http.BaseAddress!, path).AbsoluteUri}>)";
}
