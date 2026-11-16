using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/knowledge-checks/live")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LiveKnowledgeChecksController(AppDbContext db) : ControllerBase
{
    internal const string JoinRatePolicy = "live-quiz-join";
    private const int PlayerLimit = 100;
    private const int TokenLength = 64;
    private static readonly JsonSerializerOptions GameJson = new(JsonSerializerDefaults.Web);

    [HttpPost]
    public async Task<IActionResult> Create(LiveQuizCreate request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (request.SourceId == Guid.Empty || request.QuestionSeconds is < 5 or > 120)
            return BadRequest(new { error = "live_invalid_settings" });

        var source = request.FromDraft
            ? await db.KnowledgeCheckQuizzes.AsNoTracking()
                .Where(row => row.Id == request.SourceId && row.OwnerSub == sub)
                .Select(row => new Source(row.Title, row.QuestionsJson)).SingleOrDefaultAsync(ct)
            : await db.KnowledgeCheckPublications.AsNoTracking()
                .Where(row => row.Id == request.SourceId && row.IsActive && row.HoldId == null)
                .Select(row => new Source(row.Version.Title, row.Version.QuestionsJson)).SingleOrDefaultAsync(ct);
        if (source is null) return NotFound(new { error = "live_source_not_found" });

        var questions = Read<KnowledgeCheckQuestion>(source.QuestionsJson);
        if (questions.Length == 0 || questions.Any(question => !IsReady(question) || question.ManualGrading))
            return BadRequest(new { error = "live_quiz_not_ready" });

        var game = LiveQuizGame.New();
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var room = new LiveKnowledgeCheck
            {
                Id = Guid.NewGuid(),
                Code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString(CultureInfo.InvariantCulture),
                HostSub = sub,
                Title = source.Title,
                QuestionSeconds = request.QuestionSeconds,
                QuestionsJson = source.QuestionsJson,
                GameJson = Write(game),
                ExpiresAtUtc = now.AddHours(6).UtcDateTime
            };
            db.LiveKnowledgeChecks.Add(room);
            try
            {
                await db.SaveChangesAsync(ct);
                return Ok(game.Project(room, questions, now, isHost: true));
            }
            catch (DbUpdateException ex) when (NotebookPeriods.IsUniqueViolation(ex))
            {
                db.Entry(room).State = EntityState.Detached;
            }
        }
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "live_code_unavailable" });
    }

    [AllowAnonymous, HttpGet("{code}")]
    public async Task<IActionResult> State(string code, CancellationToken ct)
    {
        if (!LiveQuiz.IsCode(code)) return MissingRoom();
        var hash = HashToken(Request.Headers[LiveQuiz.PlayerHeader]);
        var hostSub = hash is null && UserClaims.IsTeacher(User) ? UserClaims.GetSub(User) : null;
        if (hash is null && hostSub is null) return MissingRoom();

        var now = DateTimeOffset.UtcNow;
        var room = await FindRoom(code, now, hostSub).AsNoTracking().SingleOrDefaultAsync(ct);
        if (room is null) return MissingRoom();
        var game = Game(room);
        var player = Player(game, hash);
        if (hostSub is null && player is null) return MissingRoom();

        return Ok(game.Project(room, Questions(room), now, hostSub is not null, player));
    }

    [AllowAnonymous, EnableRateLimiting(JoinRatePolicy), HttpPost("{code}/join")]
    [RequestSizeLimit(4096)]
    public Task<IActionResult> Join(string code, LiveQuizJoin request, CancellationToken ct)
    {
        if (request.PlayerToken is not null)
        {
            var hash = HashToken(request.PlayerToken);
            if (hash is null) return Task.FromResult<IActionResult>(MissingRoom());
            return Mutate(code, null, hash, (room, game, questions, now) =>
            {
                var player = Player(game, hash);
                return player is null ? new(null, MissingRoom()) : new(null,
                    Ok(new LiveQuizJoined(request.PlayerToken, game.Project(room, questions, now, isHost: false, player))));
            }, ct);
        }

        var nickname = NormalizeNickname(request.Nickname);
        if (nickname is null)
            return Task.FromResult<IActionResult>(BadRequest(new { error = "live_invalid_nickname" }));

        return Mutate(code, null, null, (room, game, questions, now) =>
        {
            var phase = game.PhaseAt(now);
            if (phase == LiveQuiz.Finished) return new(null, ConflictError("live_finished"));
            if (game.Locked) return new(null, ConflictError("live_locked"));
            if (game.Players.Length >= PlayerLimit) return new(null, ConflictError("live_full"));
            if (game.Players.Any(player => player.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)))
                return new(null, ConflictError("live_nickname_taken"));

            var token = RandomNumberGenerator.GetHexString(TokenLength);
            var joined = new LiveQuizParticipant(Guid.NewGuid(), nickname, HashToken(token)!);
            // Preserve a reveal already reached before a late arrival.
            var next = game with { Phase = phase, Players = [.. game.Players, joined] };
            return new(next, Ok(new LiveQuizJoined(token, next.Project(room, questions, now, isHost: false, joined))));
        }, ct);
    }

    [AllowAnonymous, HttpPost("{code}/answer")]
    [RequestSizeLimit(8192)]
    public Task<IActionResult> Answer(string code, LiveQuizAnswer request, CancellationToken ct)
    {
        var received = DateTimeOffset.UtcNow;
        var hash = HashToken(Request.Headers[LiveQuiz.PlayerHeader]);
        if (hash is null) return Task.FromResult<IActionResult>(MissingRoom());

        return Mutate(code, null, hash, (room, game, questions, now) =>
        {
            var player = Player(game, hash);
            if (player is null) return new(null, MissingRoom());
            if (request.QuestionIndex < 0 || request.QuestionIndex != game.QuestionIndex)
                return new(null, ConflictError("live_question_closed"));

            var question = questions[game.QuestionIndex];
            if (!TryNormalizeAnswer(question, new(question.Id, request.SelectedOptions, request.Text), out var answer)
                || answer is null)
                return new(null, BadRequest(new { error = "live_invalid_answer" }));

            var previous = game.Answers.FirstOrDefault(item => item.PlayerId == player.Id);
            if (previous is not null)
                return previous.SelectedOptions.SequenceEqual(answer.SelectedOptions) && previous.Text == answer.Text
                    ? new(null, Ok(game.Project(room, questions, now, isHost: false, player)))
                    : new(null, ConflictError("live_already_answered"));

            if (game.Phase != LiveQuiz.Question || game.DeadlineUtc is not DateTimeOffset deadline || received >= deadline)
                return new(null, ConflictError("live_question_closed"));

            var correct = Grade(question, answer).Correct == true;
            var points = correct ? LiveQuizGame.Points(deadline, room.QuestionSeconds, received) : 0;
            var submitted = new LiveQuizRoundAnswer(player.Id, answer.SelectedOptions, answer.Text, correct, points);
            var next = game with { Answers = [.. game.Answers, submitted] };
            return new(next, Ok(next.Project(room, questions, now, isHost: false, player)));
        }, ct);
    }

    [HttpPost("{code}/advance")]
    [RequestSizeLimit(2048)]
    public Task<IActionResult> Advance(string code, LiveQuizAdvance request, CancellationToken ct) =>
        ChangeAsHost(code, (room, game, questions, now) =>
        {
            var phase = game.PhaseAt(now);
            if (phase != request.Phase || game.QuestionIndex != request.QuestionIndex)
                return new(null, ConflictError("live_stale_state"));

            var next = phase switch
            {
                LiveQuiz.Lobby when game.Players.Length > 0 => game.OpenQuestion(now, room.QuestionSeconds),
                LiveQuiz.Question => game with { Phase = LiveQuiz.Reveal },
                LiveQuiz.Reveal => game.ShowLeaderboard(),
                LiveQuiz.Leaderboard when game.QuestionIndex + 1 < questions.Length => game.OpenQuestion(now, room.QuestionSeconds),
                LiveQuiz.Leaderboard => game.Finish(),
                _ => null
            };
            if (next is null)
                return new(null, ConflictError(phase == LiveQuiz.Lobby ? "live_no_players" : "live_finished"));
            return new(next, Ok(next.Project(room, questions, now, isHost: true)));
        }, ct);

    [HttpPost("{code}/end")]
    public Task<IActionResult> End(string code, CancellationToken ct) =>
        ChangeAsHost(code, (room, game, questions, now) =>
        {
            var next = game.Phase == LiveQuiz.Finished ? game : game.Finish();
            return new(next == game ? null : next, Ok(next.Project(room, questions, now, isHost: true)));
        }, ct);

    [HttpPost("{code}/lock")]
    [RequestSizeLimit(2048)]
    public Task<IActionResult> Lock(string code, LiveQuizLock request, CancellationToken ct) =>
        ChangeAsHost(code, (room, game, questions, now) =>
        {
            if (game.Phase == LiveQuiz.Finished) return new(null, ConflictError("live_finished"));
            var next = game with { Locked = request.Locked };
            return new(next == game ? null : next, Ok(next.Project(room, questions, now, isHost: true)));
        }, ct);

    [HttpPost("{code}/kick")]
    [RequestSizeLimit(2048)]
    public Task<IActionResult> Kick(string code, LiveQuizKick request, CancellationToken ct) =>
        ChangeAsHost(code, (room, game, questions, now) =>
        {
            if (game.Phase != LiveQuiz.Lobby) return new(null, ConflictError("live_stale_state"));
            var remaining = game.Players.Where(player => player.Id != request.PlayerId).ToArray();
            var next = game with { Players = remaining };
            return new(remaining.Length == game.Players.Length ? null : next,
                Ok(next.Project(room, questions, now, isHost: true)));
        }, ct);

    private Task<IActionResult> ChangeAsHost(string code, Change change, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Task.FromResult<IActionResult>(Unauthorized());
        if (!UserClaims.IsTeacher(User)) return Task.FromResult<IActionResult>(Forbid());
        return Mutate(code, sub, null, change, ct);
    }

    private async Task<IActionResult> Mutate(string code, Guid? hostSub, string? playerHash, Change change,
        CancellationToken ct)
    {
        if (!LiveQuiz.IsCode(code)) return MissingRoom();

        var query = FindRoom(code, DateTimeOffset.UtcNow, hostSub);
        if (playerHash is not null)
            query = query.Where(room => room.GameJson.Contains(playerHash));

        // Lock one room so concurrent answers and host commands evaluate the same authoritative state.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locked = await query.ExecuteUpdateAsync(set => set.SetProperty(row => row.GameJson, row => row.GameJson), ct);
        if (locked == 0) return MissingRoom();

        var room = await query.AsNoTracking().SingleAsync(ct);
        var now = DateTimeOffset.UtcNow;
        if (room.ExpiresAtUtc <= now.UtcDateTime) return MissingRoom();
        var result = change(room, Game(room), Questions(room), now);
        if (result.Game is not null)
        {
            var json = Write(result.Game);
            await db.LiveKnowledgeChecks.Where(row => row.Id == room.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.GameJson, json), ct);
            await transaction.CommitAsync(ct);
        }
        return result.Response;
    }

    private IQueryable<LiveKnowledgeCheck> FindRoom(string code, DateTimeOffset now, Guid? hostSub)
    {
        var matches = db.LiveKnowledgeChecks.Where(room => room.Code == code && room.ExpiresAtUtc > now.UtcDateTime);
        return hostSub is Guid sub ? matches.Where(room => room.HostSub == sub) : matches;
    }

    private static string? HashToken(string? token) => token is { Length: TokenLength } && token.All(char.IsAsciiHexDigit)
        ? Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)))
        : null;

    private static string? NormalizeNickname(string? value)
    {
        if (value is null || value.Length > 64) return null;
        var nickname = value.Trim().Normalize(NormalizationForm.FormKC);
        return nickname.Length is >= 1 and <= 24 && nickname.Any(char.IsLetterOrDigit)
            && !nickname.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format)
            ? nickname : null;
    }

    private static LiveQuizParticipant? Player(LiveQuizGame game, string? hash) =>
        hash is null ? null : game.Players.FirstOrDefault(player => player.TokenHash == hash);
    private static KnowledgeCheckQuestion[] Questions(LiveKnowledgeCheck room) => Read<KnowledgeCheckQuestion>(room.QuestionsJson);
    private static LiveQuizGame Game(LiveKnowledgeCheck room) => JsonSerializer.Deserialize<LiveQuizGame>(room.GameJson, GameJson)!;
    private NotFoundObjectResult MissingRoom() => NotFound(new { error = "live_not_found" });
    private ConflictObjectResult ConflictError(string error) => Conflict(new { error });

    private sealed record Source(string Title, string QuestionsJson);
    private sealed record Mutation(LiveQuizGame? Game, IActionResult Response);
    private delegate Mutation Change(LiveKnowledgeCheck room, LiveQuizGame game, KnowledgeCheckQuestion[] questions, DateTimeOffset now);
}
