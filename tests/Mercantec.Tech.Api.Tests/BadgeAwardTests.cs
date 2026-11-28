using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class ActionCenterTests
{
    [Fact]
    public async Task Badge_review_requires_the_right_hold_and_one_approval_gives_one_private_notification()
    {
        BadgesController Badges(Guid sub, bool teacher = false) => new(_db, new CourseAccess(_db))
            { ControllerContext = Api(sub, teacher).ControllerContext };
        var pupil = Badges(_peer);
        var teacher = Badges(_teacher, true);
        var wrongTeacher = Badges(_otherTeacher, true);
        var application = new BadgeApplication(_hold.Id, 1, "Jeg skrev observationerne fra vores spiltest.");
        Assert.IsType<BadRequestResult>(await pupil.Apply("wordle-win", application, default));
        Assert.IsType<ConflictResult>(await pupil.Apply("playtest", application with { Rank = 2 }, default));
        Assert.IsType<ForbidResult>(await pupil.Apply("playtest", application with { HoldId = _otherHold.Id }, default));
        Assert.IsType<NoContentResult>(await pupil.Apply("playtest", application, default));
        Assert.IsType<ConflictResult>(await pupil.Apply("playtest", application, default));
        var requests = Assert.IsType<BadgeReview[]>(Assert.IsType<OkObjectResult>(await teacher.Requests(default)).Value);
        var request = Assert.Single(requests);
        Assert.IsType<NotFoundResult>(await wrongTeacher.Review(request.Id, new(true), default));
        Assert.IsType<NoContentResult>(await teacher.Review(request.Id, new(false), default));
        Assert.IsType<NoContentResult>(await pupil.Apply("playtest", application, default));
        var second = Assert.Single(Assert.IsType<BadgeReview[]>(Assert.IsType<OkObjectResult>(await teacher.Requests(default)).Value));
        Assert.NotEqual(request.Id, second.Id);
        Assert.IsType<NoContentResult>(await teacher.Review(second.Id, new(true), default));
        Assert.IsType<NotFoundResult>(await teacher.Review(second.Id, new(true), default));
        async Task<ActionCenterItem[]> Notices(Guid sub) =>
            Read(await Api(sub).Get(null, default, 2, badges: true)).Items.Where(i => i.Kind == "badge-award").ToArray();
        Assert.Empty(await Notices(_student));
        var notice = Assert.Single(await Notices(_peer));
        Assert.Equal("Playtester", notice.Title);
        var receipt = new ActionCenterReadRequest([new("badge-award", notice.ResourceId)]);
        await Api(_student).Read(receipt, default);
        Assert.Single(await Notices(_peer));
        await Api(_peer).Read(receipt, default);
        Assert.Empty(await Notices(_peer));
        var collection = Assert.IsType<BadgeCollection>(Assert.IsType<OkObjectResult>(await pupil.Mine(default)).Value);
        Assert.Equal("earned", Assert.Single(collection.Entries).Status);
    }
}
