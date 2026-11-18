using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Electives;
using Mercantec.Tech.Web.Pages.Electives;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class ElectiveRosterTests
{
    [Fact]
    public async Task Teacher_can_use_a_reserved_seat_then_cancel_or_confirm_removal_in_the_same_roster()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        using var api = new RosterApi();
        using var http = new HttpClient(api) { BaseAddress = new("https://fixture.test/api/") };
        context.Services.AddSingleton(new ElectiveClient(http));

        var roster = context.Render<ElectiveManagement>(parameters => parameters
            .Add(p => p.Items, [api.Item])
            .Add(p => p.InitialRosterId, api.Item.Id));
        var search = roster.WaitForElement("input[aria-describedby=roster-search-help]");
        search.Input("A");
        Assert.Empty(api.Queries);
        search.Input("QA");
        roster.WaitForElement("button[aria-label='Tilføj Ada QA']").Click();
        roster.WaitForAssertion(() => Assert.Contains("Navngivne deltagere (1)", roster.Markup));
        Assert.Equal(["QA"], api.Queries);
        Assert.True(api.UsedReserved);
        Assert.Contains("1 af 1 pladser optaget. 0 reserveret uden navn.", roster.Markup);
        Assert.Contains("Lectio-id 21", Assert.Single(roster.FindAll(".roster-list li")).TextContent);
        roster.Find("button[aria-label='Fjern Ada QA, Lectio-id 21']").Click();
        roster.Find(".roster-confirm .secondary").Click();
        Assert.False(api.Removed);
        roster.Find("button[aria-label='Fjern Ada QA, Lectio-id 21']").Click();
        roster.Find(".roster-confirm .action").Click();
        roster.WaitForAssertion(() => Assert.Contains("Navngivne deltagere (0)", roster.Markup));
        Assert.True(api.Removed);
        Assert.Contains("0 af 1 pladser optaget", roster.Markup);
    }

    private sealed class RosterApi : HttpMessageHandler
    {
        public ElectiveView Item { get; private set; } = new(Guid.NewGuid(), "Et hold", "", "", null, null, null, null,
            1, 1, "closed", null, null, 1, 1, false, false, false);
        public List<string> Queries { get; } = [];
        public bool UsedReserved { get; private set; }
        public bool Removed { get; private set; }

        private ElectiveParticipant[] _members = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!;
            if (url.AbsolutePath.EndsWith("/lectio-students"))
            {
                Queries.Add(Uri.UnescapeDataString(url.Query["?q=".Length..]));
                return Ok(new[] { new ElectiveLectioStudent("21", "Ada QA", "H3") });
            }

            if (url.AbsolutePath.EndsWith($"/{Item.Id}/participants"))
                return Ok(_members);

            Assert.EndsWith($"/{Item.Id}/participants/lectio/21", url.AbsolutePath);
            if (request.Method == HttpMethod.Put)
            {
                var input = await request.Content!.ReadFromJsonAsync<AddElectiveParticipant>(cancellationToken);
                UsedReserved = input!.UseReservedSeat;
                _members = [new(null, "Ada QA", DateTimeOffset.UtcNow, "21", Guid.NewGuid())];
                Item = Item with { ReservedSeats = 0 };
            }
            else
            {
                Assert.Equal(HttpMethod.Delete, request.Method);
                Removed = true;
                _members = [];
                Item = Item with { EnrollmentCount = 0 };
            }

            return Ok(Item);
        }

        private static HttpResponseMessage Ok<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
