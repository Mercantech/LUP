using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Games;
using Mercantec.Tech.Web.Pages.SocialGames;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class SocialGameInviteUiTests
{
    [Fact]
    public async Task Host_looks_up_and_invites_a_player_in_the_displayed_room()
    {
        var room = new SocialRoomView("042173", Guid.NewGuid(), true, [], new([], null, true), 1, null, null, [], true);
        var target = new SocialInviteCandidate(Guid.NewGuid(), "Sofie", true);
        var handler = new RoomHandler(room, target);
        using var context = await LearningUiTestContext.CreateAsync(handler,
            configure: services => services.AddSingleton<SocialGamesClient>());
        var rendered = context.Render<Room>(parameters => parameters.Add(page => page.Code, room.Code));

        var invite = rendered.WaitForElement("button[aria-label='Invitér Sofie']");
        invite.Click();

        rendered.WaitForAssertion(() => Assert.Equal(target.UserSub, handler.Invited));
    }

    private sealed class RoomHandler(SocialRoomView room, SocialInviteCandidate target) : HttpMessageHandler
    {
        public Guid? Invited { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var roomPath = $"/api/social-games/rooms/{room.Code}";
            if (path == roomPath && request.Method == HttpMethod.Get)
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(room) };
            if (path == $"{roomPath}/invitees" && request.Method == HttpMethod.Get)
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { target }) };
            if (path == $"{roomPath}/invitations" && request.Method == HttpMethod.Post)
            {
                Invited = (await request.Content!.ReadFromJsonAsync<SocialInviteRequest>(ct))?.UserSub;
                return new(HttpStatusCode.NoContent);
            }
            return new(HttpStatusCode.NotFound);
        }
    }
}
