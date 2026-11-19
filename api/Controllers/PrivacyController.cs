using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/privacy")]
[Authorize]
public class PrivacyController(AppDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Ret til indsigt (GDPR art. 15): eksporter egne platform-data.
    /// Auth-konto (password, MFA m.m.) ligger hos Mercantec Auth — ikke her.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var package = await BuildDataPackageAsync(ct);
        var json = JsonSerializer.Serialize(package, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"mercantec-tech-data-{package.Sub ?? "unknown"}-{DateTime.UtcNow:yyyyMMdd}.json";
        return File(bytes, "application/json", fileName);
    }

    /// <summary>
    /// Sletteanmodning (GDPR art. 17): bekræfter anmodning. Fuld sletning i Auth/Lectio er out of scope.
    /// </summary>
    [HttpPost("deletion-request")]
    public IActionResult RequestDeletion([FromBody] DeletionRequestBody? body)
    {
        var subGuid = UserClaims.GetSub(User);
        if (subGuid is null)
            return Unauthorized(new { error = "missing_sub" });
        var sub = subGuid.Value.ToString();

        var reference = $"del-{DateTime.UtcNow:yyyyMMddHHmmss}-{sub[..8]}";

        // I v1 logger vi kun — ingen persistens før domæne-DB (#4).
        Console.WriteLine(
            $"[privacy] deletion-request ref={reference} sub={sub} reason={body?.Reason ?? "(none)"} utc={DateTime.UtcNow:O}");

        return Ok(new
        {
            received = true,
            reference,
            requestedAtUtc = DateTime.UtcNow,
            sub,
            nextSteps = new[]
            {
                "Platform-data i Mercantec.tech, herunder dit valgte visningsnavn og profilfoto, slettes manuelt af drift, når anmodningen er verificeret (indtil automatik findes).",
                "Identitetskonto (login, MFA, roller) administreres i Mercantec Auth — kontakt Auth/drift for sletning dér.",
                "Lectio-skoleforløb ligger uden for denne app (Lectio-Collector / Lectio)."
            },
            contact = "mags@mercantec.dk"
        });
    }

    private async Task<PrivacyExportPackage> BuildDataPackageAsync(CancellationToken ct)
    {
        var subGuid = UserClaims.GetSub(User);
        var sub = subGuid?.ToString();
        var name = User.FindFirstValue("name");
        var email = User.FindFirstValue("email");
        var loginMethod = User.FindFirstValue("login_method");
        var roles = User.Claims
            .Where(c =>
                c.Type is "role" or "roles"
                || c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r)
            .ToArray();

        var safeClaims = User.Claims
            .Where(c => !IsSensitiveClaim(c.Type))
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Value).Distinct().ToArray());

        object[] memberships = [];
        string? displayName = null;
        object[] gifts = [];
        object? giftProfile = null;
        object? avatarProfile = null;
        object? profilePhoto = null;
        object[] studentDirectoryDetails = [];
        object[] pollResponses = [];
        object[] liveCourseAnswers = [];
        object[] liveCourseVotes = [];
        object[] liveCourseReactions = [];
        object[] electiveEnrollments = [];
        object[] actionReadReceipts = [];
        object[] uploadedGames = [];
        object[] wordlePlays = [];
        object[] dailyGamePlays = [];
        object[] knowledgeCheckAttempts = [];
        var dailyRankingVisible = false;
        if (subGuid is not null)
        {
            displayName = await db.ProfileNames.AsNoTracking().Where(profile => profile.UserSub == subGuid.Value)
                .Select(profile => profile.DisplayName).SingleOrDefaultAsync(ct);
            memberships = await db.Memberships
                .AsNoTracking()
                .Where(m => m.UserSub == subGuid.Value)
                .Include(m => m.Hold)
                .ThenInclude(h => h.Forloeb)
                .Select(m => (object)new
                {
                    m.Id,
                    m.HoldId,
                    holdName = m.Hold.Name,
                    forloebId = m.Hold.ForloebId,
                    forloebName = m.Hold.Forloeb.Name,
                    m.Source,
                    m.CreatedAtUtc
                })
                .ToArrayAsync(ct);
        }

        if (subGuid is Guid giftSub)
        {
            studentDirectoryDetails = await db.StudentDirectoryDetails.AsNoTracking().Where(details => details.UserSub == giftSub)
                .Select(details => (object)new { details.Placement, details.CurrentSchoolPeriod,
                    details.WorkplaceName, details.LastPassedSchoolPeriod, details.UpdatedAtUtc }).ToArrayAsync(ct);
            profilePhoto = await db.ProfilePhotos.AsNoTracking().Where(photo => photo.UserSub == giftSub)
                .Select(photo => new { photo.ContentType, photo.SizeBytes, photo.UpdatedAtUtc,
                    downloadPath = "/api/student-photos/" + giftSub.ToString() }).SingleOrDefaultAsync(ct);
            dailyRankingVisible = await db.DailyGameRankingPreferences.AsNoTracking()
                .AnyAsync(preference => preference.UserSub == giftSub, ct);
            uploadedGames = await db.UploadedGames.AsNoTracking()
                .Where(game => game.OwnerSub == giftSub && game.FileAsset.OwnerSub == giftSub)
                .OrderBy(game => game.CreatedAtUtc)
                .Select(game => (object)new { game.Id, game.Title, game.FileAssetId, game.FileAsset.OriginalName, game.EntryPoint, game.CreatedAtUtc })
                .ToArrayAsync(ct);
            var rounds = await db.WordlePlays.AsNoTracking().Where(play => play.UserSub == giftSub)
                .OrderBy(play => play.Date).Select(play => new { play.Date, play.GuessesJson, play.Status }).ToArrayAsync(ct);
            wordlePlays = rounds.Select(play => (object)new
            {
                play.Date, guesses = JsonSerializer.Deserialize<string[]>(play.GuessesJson), status = play.Status.ToString()
            }).ToArray();
            var dailyRounds = await db.DailyGamePlays.AsNoTracking().Where(play => play.UserSub == giftSub)
                .OrderBy(play => play.Date).ThenBy(play => play.Game).ToArrayAsync(ct);
            dailyGamePlays = dailyRounds.Select(play =>
            {
                var state = DailyGamesController.ReadState(play);
                return (object)new
                {
                    play.Game, play.Date, status = play.Status.ToString(), guesses = state.Guesses,
                    hintsUsed = state.HintCount, expression = state.Expression, value = state.Value
                };
            }).ToArray();
            avatarProfile = await db.AvatarProfiles.AsNoTracking().Where(profile => profile.UserSub == giftSub)
                .Select(profile => new
                {
                    selectedAvatarId = profile.SelectedAvatarId,
                    ownedAvatarIds = profile.Avatars.OrderBy(owned => owned.AvatarId).Select(owned => owned.AvatarId).ToArray(),
                    gold = profile.Gold,
                    level = profile.Level,
                    ownedItemIds = profile.Items.OrderBy(owned => owned.ItemId).Select(owned => owned.ItemId).ToArray(),
                    revision = profile.Revision
                }).SingleOrDefaultAsync(ct);
            actionReadReceipts = await db.ActionReadReceipts.AsNoTracking().Where(r => r.UserSub == giftSub)
                .Select(r => (object)new { r.UserSub, r.Kind, r.ResourceId, r.ReadAtUtc }).ToArrayAsync(ct);
            pollResponses = await db.PollResponses.AsNoTracking().Where(r => r.UserSub == giftSub)
                .Select(r => (object)new { r.PollId, r.Poll.Question, r.Choice, r.Text, r.UpdatedAtUtc }).ToArrayAsync(ct);
            liveCourseAnswers = await db.LiveCourseEntries.AsNoTracking().Where(answer => answer.UserSub == giftSub)
                .Select(answer => (object)new { answer.Id, answer.RoundId, answer.Choice, answer.Text, answer.Approved, answer.UpdatedAtUtc })
                .ToArrayAsync(ct);
            liveCourseVotes = await db.LiveCourseVotes.AsNoTracking().Where(vote => vote.UserSub == giftSub)
                .Select(vote => (object)new { vote.EntryId }).ToArrayAsync(ct);
            liveCourseReactions = await db.LiveCourseReactions.AsNoTracking().Where(reaction => reaction.UserSub == giftSub)
                .Select(reaction => (object)new { reaction.SessionId, reaction.Emoji, reaction.UpdatedAtUtc }).ToArrayAsync(ct);
            var attempts = await db.KnowledgeCheckAttempts.AsNoTracking().Where(attempt => attempt.UserSub == giftSub)
                .OrderBy(attempt => attempt.StartedAtUtc)
                .Select(attempt => new
                {
                    attempt.Id, attempt.PublicationId, attempt.Publication.Version.Title, attempt.Publication.VersionId,
                    attempt.Status, attempt.StartedAtUtc, attempt.SubmittedAtUtc, attempt.AnswersJson, attempt.Score,
                    FeedbackVisible = attempt.Publication.FeedbackMode == KnowledgeCheckRules.AfterSubmit || attempt.Publication.FeedbackReleased
                }).ToArrayAsync(ct);
            knowledgeCheckAttempts = attempts.Select(attempt => (object)new
            {
                attempt.Id, attempt.PublicationId, attempt.Title, attempt.VersionId, attempt.Status, attempt.StartedAtUtc,
                attempt.SubmittedAtUtc, answers = KnowledgeCheckRules.Read<KnowledgeCheckAnswer>(attempt.AnswersJson),
                score = attempt.FeedbackVisible ? attempt.Score : null
            }).ToArray();
            electiveEnrollments = await db.ElectiveEnrollments.AsNoTracking().Where(e => e.UserSub == giftSub)
                .Select(e => (object)new { e.ElectiveId, e.Elective.Title, e.DisplayName, e.EnrolledAtUtc }).ToArrayAsync(ct);
            gifts = await db.ChristmasGifts.AsNoTracking().Where(g => g.SenderSub == giftSub || g.RecipientSub == giftSub)
                .OrderBy(g => g.CreatedAtUtc).Select(g => (object)new { g.Id, g.SenderSub, g.RecipientSub, g.SenderName, g.RecipientName, g.Kind, g.Wrapping, g.Theme, g.Message, g.CreatedAtUtc, g.OpenedAtUtc }).ToArrayAsync(ct);
            giftProfile = await db.GiftContacts.AsNoTracking().Where(g => g.UserSub == giftSub)
                .Select(g => new { g.UserSub, g.DisplayName, g.IsTeacher }).FirstOrDefaultAsync(ct);
        }

        return new PrivacyExportPackage(
            ExportedAtUtc: DateTime.UtcNow,
            Source: "mercantec.tech",
            Sub: sub,
            Profile: new
            {
                name,
                displayName,
                email,
                loginMethod,
                roles
            },
            PlatformRecords: new
            {
                memberships,
                gifts,
                giftProfile,
                avatarProfile,
                profilePhoto,
                studentDirectoryDetails,
                uploadedGames,
                wordlePlays,
                dailyGamePlays,
                dailyRankingVisible,
                pollResponses,
                liveCourseAnswers,
                liveCourseVotes,
                liveCourseReactions,
                knowledgeCheckAttempts,
                electiveEnrollments,
                actionReadReceipts,
                submissions = Array.Empty<object>(),
                deletionRequests = Array.Empty<object>()
            },
            Claims: safeClaims,
            Notes: new[]
            {
                "Eksporten indeholder data kendt af Mercantec.tech API ud fra din JWT og platform-DB — ikke Auth-backendens fulde profil.",
                "Tokens, refresh-tokens og secrets eksporteres aldrig."
            }
        );
    }

    private static bool IsSensitiveClaim(string type)
    {
        var t = type.ToLowerInvariant();
        return t.Contains("password")
               || t.Contains("secret")
               || t.Contains("token")
               || t.Contains("refresh")
               || t.Contains("credential")
               || t.Contains("otp")
               || t.Contains("mfa");
    }

    public sealed record DeletionRequestBody(string? Reason);

    private sealed record PrivacyExportPackage(
        DateTime ExportedAtUtc,
        string Source,
        string? Sub,
        object Profile,
        object PlatformRecords,
        Dictionary<string, string[]> Claims,
        string[] Notes);
}
