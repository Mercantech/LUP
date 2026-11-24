using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class HomeGameTests
{
    [Fact]
    public async Task Teacher_can_try_all_equipment_without_spending_gold_and_students_can_use_starter_devices()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AvatarProfiles.Add(new AvatarProfile { UserSub = teacher, Gold = 0 });
        await db.SaveChangesAsync();
        var teacherHome = As(db, teacher, "Teacher");
        var state = Read(await teacherHome.Get(default));
        Assert.True(state.IsTeacherSandbox);
        Assert.True(Read(await As(db, bob, "Admin").Get(default)).IsTeacherSandbox);
        Assert.Equal(0, state.Gold);

        var before = state;
        var purchase = new HomeBuildRequest(Guid.NewGuid(), state.Layout.Revision,
            new("buy", DefinitionId: HomeCatalog.DaylightId, Placement: new(new(7, 0), 180, new(7, 0, true))));
        state = Read(await teacherHome.Build(purchase, default));
        Assert.Equal(0, state.Gold);
        var previewDevice = Assert.Single(state.Layout.Items, item => item.DefinitionId == HomeCatalog.DaylightId && !item.Starter);
        Assert.Equal(0, previewDevice.GoldPaid);
        state = Read(await teacherHome.Build(new(Guid.NewGuid(), state.Layout.Revision, new("undo")), default));
        Assert.Equal(0, state.Gold);
        Assert.Equal(before.Layout.Items, state.Layout.Items);

        state = await Prepare(teacherHome);
        var finished = await Finish(teacherHome, await Start(teacherHome, state));
        Assert.True(finished.Run.IsTeacherSandbox);
        Assert.Equal("completed", finished.Run.Status);
        Assert.False(finished.Run.Assessment);
        Assert.Null(finished.Run.Passed);
        Assert.Equal(0, finished.GoldAwarded);
        state = Read(await teacherHome.Get(default));
        Assert.True(state.LastRun?.IsTeacherSandbox);
        Assert.Empty(state.Progress);
        Assert.Equal(0, state.Gold);

        foreach (var (definitionId, x) in new[] { (HomeCatalog.DaylightId, 7), (HomeCatalog.PresenceId, 6), (HomeCatalog.SwitchId, 8) })
        {
            var item = state.Layout.Items.Single(item => item.DefinitionId == definitionId);
            state = Read(await teacherHome.Build(new(Guid.NewGuid(), state.Layout.Revision,
                new("place", item.Id, Placement: new(new(x, 0), 180, new(x, 0, true)))), default));
        }
        Assert.Equal(404, Status(await As(db, alice).Status(finished.Run.Id, default)));

        var learner = As(db, alice);
        var learnerState = Read(await learner.Get(default));
        Assert.False(learnerState.IsTeacherSandbox);
        foreach (var (definitionId, x) in new[] { (HomeCatalog.DaylightId, 7), (HomeCatalog.PresenceId, 6), (HomeCatalog.SwitchId, 8) })
        {
            var item = learnerState.Layout.Items.Single(item => item.DefinitionId == definitionId);
            learnerState = Read(await learner.Build(new(Guid.NewGuid(), learnerState.Layout.Revision,
                new("place", item.Id, Placement: new(new(x, 0), 180, new(x, 0, true)))), default));
        }
        Assert.Equal(AvatarEconomy.InitialGold, learnerState.Gold);
        Assert.Empty(learnerState.Progress);
    }
}
