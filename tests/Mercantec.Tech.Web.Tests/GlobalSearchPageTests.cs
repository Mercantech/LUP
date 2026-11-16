using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SearchPage = Mercantec.Tech.Web.Pages.Search;

namespace Mercantec.Tech.Web.Tests;

public sealed class GlobalSearchPageTests
{
    [Fact]
    public async Task Guest_can_filter_public_results_from_a_direct_link_without_using_private_search()
    {
        using var server = new SearchHandler(_ => Task.FromException<HttpResponseMessage>(
            new InvalidOperationException("Guest search made an API request.")));
        using var context = await LearningUiTestContext.CreateAsync(server);
        await context.Auth.LogoutLocalAsync();
        Navigate(context, "/search?q=MQTT&category=badges");

        var page = context.Render<SearchPage>();

        Assert.Equal("badges", page.Find("select[name=category]").GetAttribute("value"));
        Assert.Contains("/badges/mqtt", page.FindAll(".search-hit a").Select(link => link.GetAttribute("href")));
        Assert.All(page.FindAll(".search-hit a"), link => Assert.StartsWith("/badges/", link.GetAttribute("href")));
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task Changing_category_preserves_the_query_and_requests_the_new_category_before_pagination()
    {
        using var server = new SearchHandler(uri =>
        {
            var people = uri.Query.Contains("category=people", StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(people
                    ? new GlobalSearchResult([new("people", "Underviser", "Maja", "/courses/holds/majas-hold", "H2")], 1)
                    : new GlobalSearchResult([new("assignments", "Opgave", "Majas opgave", "/quests/7", "H2", "Byg et API.")], 1))
            };
        });
        using var context = await LearningUiTestContext.CreateAsync(server);
        var navigation = Navigate(context, "/search?q=Maja");
        var page = context.Render<SearchPage>();
        Assert.Contains("Majas opgave", page.Find(".search-hit").TextContent);
        Assert.Equal("all", page.Find("select").GetAttribute("value"));

        page.Find("select").Change("people");

        page.WaitForAssertion(() => Assert.Equal("Maja", page.Find(".search-hit a").TextContent));
        Assert.EndsWith("/search?q=Maja&category=people", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(2, server.Requests.Count);
        Assert.Equal("/api/search?q=Maja&category=people&offset=0&limit=20", server.Requests[^1].PathAndQuery);
        Assert.Contains("Underviser", page.Find(".search-hit-context").TextContent);
    }

    [Fact]
    public async Task API_error_has_a_retry_and_an_empty_response_has_a_distinct_status()
    {
        var failed = true;
        using var server = new SearchHandler(_ =>
        {
            if (failed)
            {
                failed = false;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return Results();
        });
        using var context = await LearningUiTestContext.CreateAsync(server);
        Navigate(context, "/search?q=ukendt");
        var page = context.Render<SearchPage>();

        Assert.Contains("Søgningen kunne ikke gennemføres", page.Find("[role=alert]").TextContent);
        Assert.DoesNotContain("Ingen resultater", page.Find("[role=status]").TextContent);
        page.Find("[role=alert] button").Click();

        page.WaitForAssertion(() => Assert.Contains("Ingen resultater", page.Find("[role=status]").TextContent));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Fact]
    public async Task Switching_to_student_preview_discards_in_flight_teacher_results()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new SearchHandler(_ => pending.Task);
        using var context = await LearningUiTestContext.CreateAsync(server, teacher: true);
        Navigate(context, "/search?q=MQTT");
        var page = context.Render<SearchPage>();
        Assert.Single(server.Requests);

        context.ViewAs.Enter("preview-student", "Elev");
        page.WaitForAssertion(() => Assert.Contains("/badges/mqtt",
            page.FindAll(".search-hit a").Select(link => link.GetAttribute("href"))));
        pending.SetResult(Results(new GlobalSearchHit("notes", "Kladde", "Privat MQTT", "/notion/private", "Lærer")));
        await page.InvokeAsync(() => Task.CompletedTask);

        Assert.Single(server.Requests);
        Assert.DoesNotContain("Privat MQTT", page.Markup);
    }

    private static NavigationManager Navigate(LearningUiTestContext context, string path)
    {
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(path);
        return navigation;
    }

    private static HttpResponseMessage Results(params GlobalSearchHit[] hits) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new GlobalSearchResult(hits, hits.Length)) };

    private sealed class SearchHandler(Func<Uri, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        public SearchHandler(Func<Uri, HttpResponseMessage> handle)
            : this(uri => Task.FromResult(handle(uri))) { }

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return handle(request.RequestUri!);
        }
    }
}
