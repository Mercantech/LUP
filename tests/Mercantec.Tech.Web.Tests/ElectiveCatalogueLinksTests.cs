using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Electives;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ElectivesPage = Mercantec.Tech.Web.Pages.Electives.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class ElectiveCatalogueLinksTests
{
    [Fact]
    public async Task Archive_keeps_official_goals_and_local_pdfs_and_omits_imported_links()
    {
        const string official = "https://www.eud.uddannelsesadministration.dk/Soeg/";
        var item = new ElectiveView(Guid.NewGuid(), "Kataloghold",
            $"Læs [programmet](https://mercantec.notion.site/page). Fagmål {official}",
            "Datagrundlag\nhttp://www.notion.so/abc", null, null, null, null, null, 0,
            "archived", "https://mercantec.notion.site/hold", null, 1, 0, false, false, false);
        using var api = new CatalogueApi(item);
        await using var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<ElectiveClient>());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/valgfag?vis=historik");

        var page = context.Render<ElectivesPage>();

        page.WaitForAssertion(() => Assert.Contains("Kataloghold", page.Markup));
        Assert.DoesNotContain("notion.", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Læs programmet.", page.Find("details").TextContent);
        Assert.Contains("Datagrundlag", page.Find("details").TextContent);
        Assert.Single(page.FindAll($"a[href='{official}']"));
        Assert.Equal(3, page.FindAll("a[href$='.pdf']").Count);
    }

    private sealed class CatalogueApi(ElectiveView item) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ElectiveWorkspace(false, [item]))
            });
    }
}
