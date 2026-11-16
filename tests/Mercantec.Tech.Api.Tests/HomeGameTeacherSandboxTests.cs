using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class HomeGameTests
{
    [Fact]
    public async Task Teacher_sandbox_opens_every_task_and_device_without_earning_gold_or_progress()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AvatarProfiles.Add(new AvatarProfile { UserSub = teacher, Gold = 0 });
        await db.SaveChangesAsync();
        var teacherHome = As(db, teacher, "Teacher");
        var state = Read(await teacherHome.Get(default));
        Assert.True(state.IsTeacherSandbox);
        Assert.Equal(6, state.HighestAvailableTask);
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
        var assessed = await Finish(teacherHome, await Start(teacherHome, state, assessment: true));
        Assert.True(assessed.Run.IsTeacherSandbox);
        Assert.True(assessed.Run.Passed);
        Assert.Equal(0, assessed.GoldAwarded);
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
        var advanced = Read(await teacherHome.Start(new(state.Layout.Revision, "python", 6), default));
        Assert.True(advanced.Run.IsTeacherSandbox);
        Assert.Equal(6, advanced.Run.Task);
        Assert.Equal(404, Status(await As(db, alice).Status(advanced.Run.Id, default)));

        var learner = As(db, alice);
        var learnerState = Read(await learner.Get(default));
        Assert.False(learnerState.IsTeacherSandbox);
        Assert.Equal(1, learnerState.HighestAvailableTask);
        var learnerSensor = learnerState.Layout.Items.Single(item => item.DefinitionId == HomeCatalog.DaylightId);
        Assert.Equal(422, Status(await learner.Build(new(Guid.NewGuid(), learnerState.Layout.Revision,
            new("place", learnerSensor.Id, Placement: new(new(7, 0), 180, new(7, 0, true)))), default)));
        Assert.Equal(403, Status(await learner.Start(new(learnerState.Layout.Revision, "python", 6), default)));
    }
}
