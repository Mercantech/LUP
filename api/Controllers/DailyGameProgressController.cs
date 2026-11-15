using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.DailyGames;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Wordle;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/games/progress")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyGameProgressController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    private readonly DailyGameProgressService progress = new(db);

    [HttpGet("{game}/history")]
    public async Task<IActionResult> History(string game, CancellationToken ct, [FromQuery] int page = 1)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        if (!DailyGameProgressService.Supports(game)) return NotFound();
        if (page is < 1 or > 1_000_000) return BadRequest(new { error = "invalid_page" });
        var today = WordleRules.LocalDate(clock.GetUtcNow());
        return Ok(await progress.History(game, subject, today, page, ct));
    }

    [HttpGet("{game}/leaderboard")]
    public async Task<IActionResult> Leaderboard(string game, CancellationToken ct, [FromQuery] string period = "day",
        [FromQuery] Guid? holdId = null)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        if (!DailyGameProgressService.Supports(game)) return NotFound();
        if (period is not ("day" or "week" or "month")) return BadRequest(new { error = "invalid_period" });
        var today = WordleRules.LocalDate(clock.GetUtcNow());
        var leaderboard = await progress.Leaderboard(game, period, holdId, User, subject, today, ct);
        return leaderboard is null ? NotFound() : Ok(leaderboard);
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        return Ok(await progress.Settings(User, subject, ct));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> SetVisibility(ChangeDailyRankingVisibility request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        if (!Guid.TryParse(Request.Headers[ProfileNameRules.SubjectHeader], out var expected) || expected != subject)
            return Conflict(new { error = "account_changed" });

        if (!await progress.SetVisibility(subject, request.Visible, ct))
            return UnprocessableEntity(new { error = "display_name_required" });
        return Ok(await progress.Settings(User, subject, ct));
    }
}
