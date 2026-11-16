using System.Text;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.HomeGame;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/holds/{holdId:guid}/home-game")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HomeTeacherController(AppDbContext db, CourseAccess access, HomeGameStore store,
    IOptions<HomeGameOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HomeTeacherOverview>> Overview(Guid holdId, CancellationToken ct)
    {
        var hold = options.Value.Enabled ? await access.AccessibleHolds(User, manageOnly: true).AsNoTracking()
            .Where(row => row.Id == holdId).Select(row => row.Name).SingleOrDefaultAsync(ct) : null;
        if (hold is null) return NotFound();

        var members = await db.Memberships.AsNoTracking().Where(row => row.HoldId == holdId && row.UserSub != null)
            .Select(row => new { Subject = row.UserSub!.Value, row.DisplayName }).ToArrayAsync(ct);
        var names = members.GroupBy(member => member.Subject).ToDictionary(group => group.Key,
            group => group.First().DisplayName ?? "Elev");
        var ids = names.Keys.ToArray();
        var profiles = await db.HomeGameProfiles.AsNoTracking().Where(row => ids.Contains(row.UserSub))
            .ToDictionaryAsync(row => row.UserSub, row => row.StateJson, ct);
        var summaries = new List<HomeTeacherStudent>();
        foreach (var (subject, name) in names)
        {
            var state = profiles.TryGetValue(subject, out var json) ? HomeGameJson.Read<HomeProfileData>(json) : null;
            var lastRun = await store.LatestRun(subject, ct);
            summaries.Add(new(subject, name, state?.Progress ?? [], lastRun?.Run.Run, state?.Reflection ?? ""));
        }

        var helpRows = await db.HomeGameHelpRequests.AsNoTracking()
            .Where(row => row.HoldId == holdId && ids.Contains(row.UserSub)).Select(row => row.DataJson).ToArrayAsync(ct);
        var requests = helpRows.Select(HomeGameJson.Read<HomeHelpEntry>)
            .OrderByDescending(entry => entry.CreatedAtUtc).ToArray();
        return Ok(new HomeTeacherOverview(holdId, hold, await Settings(holdId, ct),
            summaries.OrderBy(student => student.Name).ToArray(), requests));
    }

    [HttpGet("students/{studentSub:guid}")]
    public async Task<ActionResult<HomeTeacherStudentDetail>> Student(Guid holdId, Guid studentSub,
        CancellationToken ct, [FromQuery] Guid? runId = null)
    {
        if (!options.Value.Enabled || !await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var member = await db.Memberships.AsNoTracking().Where(row => row.HoldId == holdId && row.UserSub == studentSub)
            .Select(row => new { row.DisplayName }).FirstOrDefaultAsync(ct);
        if (member is null) return NotFound();
        var state = await store.Find(studentSub, ct);
        var run = runId is Guid requested
            ? await store.FindRun(studentSub, requested, ct)
            : await store.LatestRun(studentSub, ct);
        if (runId is not null && run is null) return NotFound();
        var draft = run is null ? null : await store.FindVersion(studentSub, run.Run.Run.ProgramVersionId, ct);
        return Ok(new HomeTeacherStudentDetail(new(studentSub, member.DisplayName ?? "Elev", state?.Progress ?? [],
            run?.Run.Run, state?.Reflection ?? ""), state?.Drafts ?? [], draft, run?.Run));
    }

    [HttpPut("settings")]
    [RequestSizeLimit(2_400_000)]
    public async Task<ActionResult<HomeTeacherSettings>> SaveSettings(Guid holdId, HomeTeacherSettings request, CancellationToken ct)
    {
        if (!options.Value.Enabled || !await access.CanManageAsync(holdId, User, ct)) return NotFound();
        if (request.Revision is < 0 or int.MaxValue
            || request.OpenThroughTask < 1 || request.OpenThroughTask > HomeCatalog.Tasks.Count
            || request.SuggestedTask < 0 || request.SuggestedTask > request.OpenThroughTask
            || request.SuggestedLanguage is { } language && !HomeGameLanguages.IsKnown(language)
            || request.StarterCode is null || request.StarterCode.Count > HomeGameLanguages.All.Count
            || request.StarterCode.Any(pair => !HomeGameLanguages.IsKnown(pair.Key) || pair.Value is null
                || Encoding.UTF8.GetByteCount(pair.Value) > HomeGameVersion.MaxSourceBytes))
            return HomeGameController.Failure(400, "INVALID_SETTINGS", "Vælg opgave, udstyr og gyldig starterkode.");

        var updated = request with { Revision = request.Revision + 1 };
        var json = HomeGameJson.Write(updated);
        int saved;
        if (request.Revision == 0)
        {
            saved = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO home_game_classrooms ("HoldId", "Revision", "SettingsJson")
                VALUES ({holdId}, {updated.Revision}, {json})
                ON CONFLICT ("HoldId") DO NOTHING
                """, ct);
        }
        else
        {
            saved = await db.HomeGameClassrooms.Where(row => row.HoldId == holdId && row.Revision == request.Revision)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, updated.Revision)
                    .SetProperty(row => row.SettingsJson, json), ct);
        }

        return saved == 1 ? Ok(updated) : HomeGameController.Failure(409, "REVISION_CONFLICT",
            "Holdets opgaver er ændret et andet sted. Hent den seneste version igen.");
    }

    private async Task<HomeTeacherSettings> Settings(Guid holdId, CancellationToken ct)
    {
        var json = await db.HomeGameClassrooms.AsNoTracking().Where(row => row.HoldId == holdId)
            .Select(row => row.SettingsJson).SingleOrDefaultAsync(ct);
        return json is null ? HomeGameStore.DefaultSettings : HomeGameJson.Read<HomeTeacherSettings>(json);
    }
}
