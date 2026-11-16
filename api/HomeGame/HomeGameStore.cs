using System.Security.Cryptography;
using System.Text;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.HomeGame;

public sealed class HomeGameStore(AppDbContext db)
{
    internal static readonly HomeTeacherSettings DefaultSettings = new(0, 1, 0, null,
        new Dictionary<string, string>());

    internal async Task<HomeProfileData> GetOrCreate(Guid subject, CancellationToken ct)
    {
        var state = await Find(subject, ct);
        if (state is not null) return state;

        var starter = new HomeProfileData(HomeCatalog.CreateStarter(), [], []);
        var serialized = HomeGameJson.Write(starter);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO home_game_profiles ("UserSub", "Revision", "StateJson")
            VALUES ({subject}, {starter.Layout.Revision}, {serialized})
            ON CONFLICT ("UserSub") DO NOTHING
            """, ct);
        return (await Find(subject, ct))!;
    }

    internal async Task<HomeProfileData?> Find(Guid subject, CancellationToken ct)
    {
        var json = await db.HomeGameProfiles.AsNoTracking().Where(row => row.UserSub == subject)
            .Select(row => row.StateJson).SingleOrDefaultAsync(ct);
        return json is null ? null : HomeGameJson.Read<HomeProfileData>(json);
    }

    internal async Task<bool> Write(Guid subject, int expectedRevision, HomeProfileData data, CancellationToken ct)
    {
        var serialized = HomeGameJson.Write(data);
        return await db.HomeGameProfiles.Where(row => row.UserSub == subject && row.Revision == expectedRevision)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, data.Layout.Revision)
                .SetProperty(row => row.StateJson, serialized), ct) == 1;
    }

    internal Task<int> Revision(Guid subject, CancellationToken ct) =>
        db.HomeGameProfiles.AsNoTracking().Where(row => row.UserSub == subject)
            .Select(row => row.Revision).SingleOrDefaultAsync(ct);

    internal async Task<HomeRunReplay?> LatestRun(Guid subject, CancellationToken ct)
    {
        var query = db.HomeGameRuns.AsNoTracking().Where(row => row.UserSub == subject);
        var json = db.Database.IsNpgsql()
            ? await query.OrderByDescending(row => row.StartedAtUtc).Select(row => row.RunJson).FirstOrDefaultAsync(ct)
            : (await query.Select(row => new { row.StartedAtUtc, row.RunJson }).ToArrayAsync(ct))
                .OrderByDescending(row => row.StartedAtUtc).Select(row => row.RunJson).FirstOrDefault();
        return json is null ? null : HomeGameJson.Read<HomeStoredRun>(json).Replay;
    }

    internal async Task<HomeRunReplay?> FindRun(Guid subject, Guid runId, CancellationToken ct)
    {
        var json = await db.HomeGameRuns.AsNoTracking().Where(row => row.UserSub == subject && row.Id == runId)
            .Select(row => row.RunJson).SingleOrDefaultAsync(ct);
        return json is null ? null : HomeGameJson.Read<HomeStoredRun>(json).Replay;
    }

    internal async Task<HomeProgramDraft?> FindVersion(Guid subject, Guid id, CancellationToken ct)
    {
        var json = await db.HomeGamePrograms.AsNoTracking().Where(row => row.UserSub == subject && row.Id == id)
            .Select(row => row.SourceJson).SingleOrDefaultAsync(ct);
        return json is null ? null : HomeGameJson.Read<HomeProgramDraft>(json);
    }

    internal async Task<HomeClassroom[]> Classrooms(Guid subject, CancellationToken ct)
    {
        var holds = await db.Memberships.AsNoTracking().Where(row => row.UserSub == subject)
            .Select(row => new { row.HoldId, row.Hold.Name }).Distinct().ToArrayAsync(ct);
        var ids = holds.Select(hold => hold.HoldId).ToArray();
        var assignments = await db.HomeGameClassrooms.AsNoTracking().Where(row => ids.Contains(row.HoldId))
            .ToDictionaryAsync(row => row.HoldId, row => row.SettingsJson, ct);
        return holds.OrderBy(hold => hold.Name).Select(hold =>
        {
            var settings = assignments.TryGetValue(hold.HoldId, out var json)
                ? HomeGameJson.Read<HomeTeacherSettings>(json) : DefaultSettings;
            return new HomeClassroom(hold.HoldId, hold.Name, settings.OpenThroughTask, settings.SuggestedTask,
                settings.SuggestedLanguage, settings.StarterCode);
        }).ToArray();
    }

    internal static int AvailableTask(HomeProfileData state, IReadOnlyList<HomeClassroom> classrooms) =>
        Math.Min(HomeCatalog.Tasks.Count, Math.Max(state.CompletedTasks + 1,
            classrooms.Select(classroom => classroom.OpenThroughTask).DefaultIfEmpty(1).Max()));

    internal static string Hash(string input) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
