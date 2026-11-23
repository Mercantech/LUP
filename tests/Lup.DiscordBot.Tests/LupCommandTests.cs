using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Lup.DiscordBot;

namespace Lup.DiscordBot.Tests;

public class LupCommandTests
{
    private const string Site = "https://lup.example/";
    private const string GamePage = Site + "games/taljagt";

    [Fact]
    public async Task SearchEncodesTheWholeTopicAsOneQueryParameter()
    {
        using var http = CreateClient();
        var reply = await new LupCommands(http).ReplyAsync("søg", "  C# & miljø?  ", CancellationToken.None);

        Assert.Contains(Site + "search?q=C%23%20%26%20milj%C3%B8%3F", reply);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public async Task SearchValidatesLengthBeforeGeneratingALink(int length, bool valid)
    {
        using var http = CreateClient();
        var topic = new string('x', length);
        var reply = await new LupCommands(http).ReplyAsync("søg", topic, CancellationToken.None);

        if (valid) Assert.Contains(Site + "search?q=" + topic, reply);
        else Assert.DoesNotContain(Site + "search?q=", reply);
    }

    [Fact]
    public async Task JoinPreservesLeadingZeroes()
    {
        using var http = CreateClient();
        var reply = await new LupCommands(http).ReplyAsync("spil", "000123", CancellationToken.None);

        Assert.Contains(Site + "spil/000123", reply);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("１２３４５６")]
    [InlineData("12/345")]
    public async Task JoinDoesNotGenerateARoomLinkForInvalidCodes(string? code)
    {
        using var http = CreateClient();
        var reply = await new LupCommands(http).ReplyAsync("spil", code, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(reply));
        Assert.DoesNotContain(Site + "spil/", reply);
    }

    [Fact]
    public async Task NumberGameShowsThePublicPuzzleIncludingRepeatedTiles()
    {
        using var http = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(Site + "api/games/daily/taljagt/daily", request.RequestUri?.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    date = "2026-11-23", nextRoundAt = "2026-11-23T23:00:00Z", hostSlug = "bilbo",
                    theme = "Regneudtryk med Bilbo Backend", tiles = new[] { 75, 9, 6, 4, 4, 2 }, target = 368
                })
            };
        });
        var reply = await new LupCommands(http).ReplyAsync("taljagt", null, CancellationToken.None);

        Assert.Contains(GamePage, reply);
        foreach (int number in new[] { 75, 9, 6, 2, 368 })
            Assert.Matches($@"\b{number}\b", reply);
        Assert.Equal(2, Regex.Matches(reply, @"\b4\b").Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "{broken")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task NumberGameLinksToLupWhenThePuzzleIsUnavailable(HttpStatusCode status, string body)
    {
        using var http = CreateClient(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var reply = await new LupCommands(http).ReplyAsync("taljagt", null, CancellationToken.None);

        Assert.Contains(GamePage, reply);
        Assert.DoesNotContain(body, reply);
    }

    [Fact]
    public async Task NumberGameTimeoutReturnsAPlayableLink()
    {
        using var http = CreateClient(_ => throw new TaskCanceledException("Simulated HTTP timeout"));
        var reply = await new LupCommands(http).ReplyAsync("taljagt", null, CancellationToken.None);

        Assert.Contains(GamePage, reply);
    }

    [Fact]
    public async Task HelpNamesTheAvailableActivities()
    {
        using var http = CreateClient();
        var reply = await new LupCommands(http).ReplyAsync("hjælp", null, CancellationToken.None);

        foreach (string command in new[] { "søg", "spil", "taljagt" })
            Assert.Contains(command, reply, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
        => new(new StubHandler(respond)) { BaseAddress = new Uri(Site) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond?.Invoke(request) ?? throw new InvalidOperationException("Unexpected HTTP request."));
    }
}
