using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.DailyGames;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Wordle;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/games/daily/{game}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyGamesController(AppDbContext db, TimeProvider clock, IConfiguration configuration) : ControllerBase
{
    private enum Action { Guess, Hint, Submit }

    [AllowAnonymous, HttpGet("daily")]
    public async Task<IActionResult> Daily(string game, CancellationToken ct)
    {
        if (!DailyGameRules.IsGame(game)) return NotFound();
        var today = WordleRules.LocalDate(clock.GetUtcNow());
        if (today < DailyGameRules.Start) return NotStarted();
        return Ok(ToDay(today, await GetPuzzle(game, today, ct)));
    }

    [HttpGet("round")]
    public async Task<IActionResult> Round(string game, CancellationToken ct)
    {
        if (!DailyGameRules.IsGame(game)) return NotFound();
        if (UserClaims.GetSub(User) is not Guid subject) return Unauthorized();
        var today = WordleRules.LocalDate(clock.GetUtcNow());
        if (today < DailyGameRules.Start) return NotStarted();
        var puzzle = await GetPuzzle(game, today, ct);
        return Ok(await ToRound(game, subject, today, puzzle, await GetPlay(game, subject, today, ct), ct));
    }

    [HttpPost("guess"), RequestSizeLimit(4096)]
    public Task<IActionResult> Guess(string game, DailyGameMove request, CancellationToken ct) => MakeMove(game, request, Action.Guess, ct);

    [HttpPost("hint"), RequestSizeLimit(4096)]
    public Task<IActionResult> Hint(string game, DailyGameMove request, CancellationToken ct) => MakeMove(game, request, Action.Hint, ct);

    [HttpPost("submit"), RequestSizeLimit(4096)]
    public Task<IActionResult> Submit(string game, DailyGameMove request, CancellationToken ct) => MakeMove(game, request, Action.Submit, ct);

    private async Task<IActionResult> MakeMove(string game, DailyGameMove request, Action action, CancellationToken ct)
    {
        if (game != (action == Action.Submit ? DailyGameIds.Numbers : DailyGameIds.Word)) return NotFound();
        if (UserClaims.GetSub(User) is not Guid subject) return Unauthorized();
        if (!Guid.TryParse(Request.Headers["X-LUP-Daily-Subject"], out var expected) || expected != subject)
            return Conflict(new { error = "account_changed" });

        var today = WordleRules.LocalDate(clock.GetUtcNow());
        if (today < DailyGameRules.Start) return NotStarted();
        var puzzle = await GetPuzzle(game, today, ct);
        var play = await GetPlay(game, subject, today, ct, track: true);
        if (request.Date != today)
            return Conflict(await ToRound(game, subject, today, puzzle, play, ct, "day_changed"));

        var state = ReadState(play);
        if (state.Requests.Contains(request.RequestId))
            return Ok(await ToRound(game, subject, today, puzzle, play, ct));
        if (request.Version != (play?.Version ?? 0))
            return Conflict(await ToRound(game, subject, today, puzzle, play, ct, "round_changed"));
        if (play is { Status: not DailyGameStatus.Playing })
            return Ok(await ToRound(game, subject, today, puzzle, play, ct));
        if (request.RequestId == Guid.Empty)
            return UnprocessableEntity(await ToRound(game, subject, today, puzzle, play, ct, "invalid_request"));

        var (status, error) = ApplyMove(puzzle, state, request, action);
        if (error is not null)
            return UnprocessableEntity(await ToRound(game, subject, today, puzzle, play, ct, error));

        if (play is null)
        {
            play = new DailyGamePlay { Game = game, Date = today, UserSub = subject };
            db.DailyGamePlays.Add(play);
        }
        state.Requests.Add(request.RequestId);
        play.StateJson = JsonSerializer.Serialize(state);
        play.Version++;
        play.Status = status;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(play).State = EntityState.Detached;
            var winner = await GetPlay(game, subject, today, ct);
            if (winner is null || winner.Version <= request.Version) throw;
            if (ReadState(winner).Requests.Contains(request.RequestId))
                return Ok(await ToRound(game, subject, today, puzzle, winner, ct));
            return Conflict(await ToRound(game, subject, today, puzzle, winner, ct, "round_changed"));
        }

        return Ok(await ToRound(game, subject, today, puzzle, play, ct));
    }

    private static (DailyGameStatus Status, string? Error) ApplyMove(DailyPuzzleData puzzle, DailyPlayState state,
        DailyGameMove request, Action action)
    {
        switch (action)
        {
            case Action.Guess:
                if (DailyGameRules.NormalizeWord(request.Word) is not { } word)
                    return (DailyGameStatus.Playing, "invalid_word");
                state.Guesses.Add(word);
                if (word == puzzle.Answer || puzzle.AcceptedAnswers?.Contains(word, StringComparer.Ordinal) == true)
                    return (DailyGameStatus.Won, null);
                return (state.Guesses.Count == DailyGameRules.MaxWordGuesses ? DailyGameStatus.Lost : DailyGameStatus.Playing, null);
            case Action.Hint:
                if (state.HintCount >= (puzzle.ExtraHints?.Length ?? 0))
                    return (DailyGameStatus.Playing, "hints_exhausted");
                state.HintCount++;
                return (DailyGameStatus.Playing, null);
            case Action.Submit:
                if (!DailyNumberExpression.TryEvaluate(puzzle.Tiles!, request.Tokens, out var value, out var expression))
                    return (DailyGameStatus.Playing, "invalid_expression");
                state.Value = value;
                state.Expression = expression;
                return (value == puzzle.Target ? DailyGameStatus.Won : DailyGameStatus.Lost, null);
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private async Task<DailyPuzzleData> GetPuzzle(string game, DateOnly date, CancellationToken ct)
    {
        var existing = await FindPuzzle(game, date, ct);
        if (existing is not null) return DeserializePuzzle(existing.DataJson);

        var data = DailyGameRules.Create(game, date, await GetKey(ct));
        var row = new DailyGamePuzzle { Game = game, Date = date, DataJson = JsonSerializer.Serialize(data) };
        db.DailyGamePuzzles.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(row).State = EntityState.Detached;
            var winner = await FindPuzzle(game, date, ct);
            if (winner is null) throw;
            return DeserializePuzzle(winner.DataJson);
        }
        return data;
    }

    private async Task<byte[]> GetKey(CancellationToken ct)
    {
        var existing = await FindKey(ct);
        if (existing is not null) return Convert.FromHexString(existing);

        var configured = configuration["DailyGames:ScheduleKey"];
        var key = string.IsNullOrWhiteSpace(configured) ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var schedule = new DailyGameSchedule { Id = 1, Key = Convert.ToHexString(key) };
        db.DailyGameSchedules.Add(schedule);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(schedule).State = EntityState.Detached;
            var winner = await FindKey(ct);
            if (winner is null) throw;
            return Convert.FromHexString(winner);
        }
        return key;
    }

    private Task<string?> FindKey(CancellationToken ct) => db.DailyGameSchedules.AsNoTracking()
        .Where(row => row.Id == 1).Select(row => row.Key).SingleOrDefaultAsync(ct);

    private Task<DailyGamePuzzle?> FindPuzzle(string game, DateOnly date, CancellationToken ct) =>
        db.DailyGamePuzzles.AsNoTracking().SingleOrDefaultAsync(row => row.Game == game && row.Date == date, ct);

    private Task<DailyGamePlay?> GetPlay(string game, Guid subject, DateOnly date, CancellationToken ct, bool track = false) =>
        (track ? db.DailyGamePlays : db.DailyGamePlays.AsNoTracking())
            .SingleOrDefaultAsync(row => row.Game == game && row.UserSub == subject && row.Date == date, ct);

    private async Task<DailyGameRound> ToRound(string game, Guid subject, DateOnly date, DailyPuzzleData puzzle,
        DailyGamePlay? play, CancellationToken ct, string? error = null)
    {
        var completed = await db.DailyGamePlays.AsNoTracking()
            .Where(row => row.UserSub == subject && row.Game == game && row.Status != DailyGameStatus.Playing && row.Date <= date)
            .OrderBy(row => row.Date).Select(row => row.Date).ToArrayAsync(ct);
        var streaks = WordleRules.Streaks(completed, date);
        var state = ReadState(play);
        var status = play?.Status ?? DailyGameStatus.Playing;
        var finished = status != DailyGameStatus.Playing;
        var hints = puzzle.ExtraHints?.Take(state.HintCount).ToArray() ?? [];
        var distance = state.Value is int value && puzzle.Target is int target ? Math.Abs(target - value) : (int?)null;
        return new(ToDay(date, puzzle, state.HintCount), play?.Version ?? 0, status, streaks.Current, streaks.Best,
            state.Guesses, hints, finished ? puzzle.Answer : null, finished ? puzzle.Explanation : null,
            finished ? puzzle.Example : null, state.Value, distance, distance is int delta ? DailyGameScoring.Numbers(delta) : null,
            state.Expression, error);
    }

    internal static DailyPlayState ReadState(DailyGamePlay? play) => play is null ? new DailyPlayState() :
        JsonSerializer.Deserialize<DailyPlayState>(play.StateJson) ?? throw new InvalidDataException("Invalid daily game play.");

    private static DailyPuzzleData DeserializePuzzle(string data) =>
        JsonSerializer.Deserialize<DailyPuzzleData>(data) ?? throw new InvalidDataException("Invalid daily puzzle.");

    private static DailyGameDay ToDay(DateOnly date, DailyPuzzleData puzzle, int hints = 0) =>
        new(date, WordleRules.NextRound(date), puzzle.Host, puzzle.Theme, puzzle.Clue, puzzle.Tiles, puzzle.Target,
            DailyGameRules.WordPattern(puzzle.Answer, hints));

    private IActionResult NotStarted() => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "game_not_started" });
}
