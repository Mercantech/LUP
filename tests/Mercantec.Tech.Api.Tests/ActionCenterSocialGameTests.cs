using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class ActionCenterTests
{
    [Fact]
    public async Task SocialGameInvitation_prioritizes_own_hold_and_only_notifies_the_recipient()
    {
        var room = new SocialGameRoom { Id = Guid.NewGuid(), Code = "186294", HostSub = _peer,
            StateJson = "{}", ExpiresAtUtc = DateTime.UtcNow.AddHours(3) };
        _db.SocialGameRooms.Add(room);
        foreach (var (sub, name) in new[] { (_teacher, "Signe fra holdet"), (_otherTeacher, "Alma fra et andet hold"),
                     (_student, "Viktor fra holdet"), (_peer, "Værten") })
            _db.NotebookProfiles.Add(new NotebookProfile { Sub = sub, DisplayName = name });
        await _db.SaveChangesAsync();
        var host = new SocialGameInvitationsController(_db) { ControllerContext = Api(_peer).ControllerContext };
        var candidates = Assert.IsType<SocialInviteCandidate[]>(Assert.IsType<OkObjectResult>(
            await host.Candidates(room.Code, null, default)).Value);
        Assert.Equal(new[] { _teacher, _student, _otherTeacher }, candidates.Select(p => p.UserSub));
        Assert.True(candidates[0].OwnHold);
        Assert.False(candidates[^1].OwnHold);
        var results = Assert.IsType<SocialInviteCandidate[]>(Assert.IsType<OkObjectResult>(
            await host.Candidates(room.Code, "ALMA", default)).Value);
        Assert.Equal(_otherTeacher, Assert.Single(results).UserSub);
        var other = new SocialGameInvitationsController(_db) { ControllerContext = Api(_student).ControllerContext };
        Assert.IsType<NotFoundResult>(await other.Candidates(room.Code, null, default));
        Assert.IsType<NotFoundObjectResult>(await other.Invite(room.Code, new(_otherTeacher), default));

        for (var i = 0; i < 2; i++) Assert.IsType<NoContentResult>(await host.Invite(room.Code, new(_otherTeacher), default));
        Assert.Equal(1, await _db.SocialGameInvitations.CountAsync());
        Assert.DoesNotContain(Read(await Api(_otherTeacher, true).Get(null, default, 2)).Items, i => i.Kind == "social-game-invite");
        var invite = Assert.Single(Read(await Api(_otherTeacher, true).Get(null, default, 2, socialGames: true)).Items,
            item => item.Kind == "social-game-invite");
        Assert.Equal(room.Code, invite.State);
        Assert.Contains("Værten", invite.Title);
        Assert.DoesNotContain(Read(await Api(_student).Get(null, default, 2, socialGames: true)).Items, i => i.Kind == "social-game-invite");
        var receipt = new ActionCenterReadRequest([new("social-game-invite", invite.ResourceId)]);
        await Api(_student).Read(receipt, default);
        Assert.Null((await _db.SocialGameInvitations.SingleAsync()).ReadAtUtc);
        await Api(_otherTeacher, true).Read(receipt, default);
        Assert.DoesNotContain(Read(await Api(_otherTeacher, true).Get(null, default, 2, socialGames: true)).Items, i => i.Kind == "social-game-invite");
    }
}
