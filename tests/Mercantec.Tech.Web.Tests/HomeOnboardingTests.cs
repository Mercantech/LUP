using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class HomeOnboardingTests
{
    [Fact]
    public async Task Preview_can_go_back_and_reopen_without_saving_dismissal()
    {
        await using var context = new BunitContext();
        using var api = new GuideApi();
        context.Services.AddSingleton(new HttpClient(api) { BaseAddress = new Uri("https://test.example/api/") });
        context.JSInterop.SetupModule("./js/modal-dialog.js").SetupVoid("open", _ => true).SetVoidResult();
        var page = context.Render<HomeOnboarding>(parameters => parameters
            .Add(p => p.Subject, Guid.NewGuid()).Add(p => p.PreviewAudience, "student"));
        Assert.Equal(0, api.Reads);
        page.Find("button").Click();
        page.WaitForAssertion(() => Assert.Contains("Trin 1 af 2", page.Markup));
        page.FindAll("button").Single(b => b.TextContent == "Næste").Click();
        Assert.Contains("Trin 2 af 2", page.Markup);
        page.FindAll("button").Single(b => b.TextContent == "Tilbage").Click();
        Assert.Contains("Trin 1 af 2", page.Markup);
        await page.Find("dialog").TriggerEventAsync("onclose", EventArgs.Empty);
        Assert.Empty(page.FindAll("dialog"));
        page.Find("button").Click();
        page.WaitForAssertion(() => Assert.Contains("Trin 1 af 2", page.Markup));
        Assert.Equal(2, api.Reads);
        Assert.Equal(0, api.Writes);
    }

    private sealed class GuideApi : HttpMessageHandler
    {
        public int Reads { get; private set; }
        public int Writes { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put) Writes++;
            else Reads++;
            Assert.Equal("?preview=student", request.RequestUri!.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { new OnboardingStepDto("Velkommen", "Find dit hold."), new OnboardingStepDto("Dine noter", "Åbn notesbogen.") })
            });
        }
    }
}
