using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.KnowledgeChecks;

internal static class LegacyKnowledgeCheckPublisher
{
    internal static async Task<int> RunCommandAsync(string[] args, IConfiguration configuration)
    {
        if (args.Length is < 3 or > 4 || args[1] != "publish-legacy"
            || !Guid.TryParse(args[2], out var owner) || owner == Guid.Empty
            || args.Length == 4 && args[3] != "--dry-run")
        {
            Console.WriteLine(Write(new { error = "usage", command = "knowledge-checks publish-legacy OWNER_SUB [--dry-run]" }));
            return 2;
        }

        var connectionString = configuration.GetConnectionString("Default") ?? configuration["CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine(Write(new { error = "database_configuration_missing" }));
            return 2;
        }

        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
            var result = await RunAsync(db, owner, dryRun: args.Length == 4);
            Console.WriteLine(Write(result));
            return result.Issues.Length == 0 ? 0 : 1;
        }
        catch (InvalidDataException)
        {
            Console.WriteLine(Write(new { error = "invalid_catalog" }));
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine(Write(new { error = "publication_failed", type = ex.GetType().Name }));
            return 1;
        }
    }

    internal static async Task<BatchResult> RunAsync(AppDbContext db, Guid owner, bool dryRun,
        CancellationToken ct = default)
    {
        var templates = LegacyKnowledgeCheckCatalog.ForOwner(owner);
        if (owner == Guid.Empty || !await IsTeacherOwnerAsync(db, owner, ct))
            return new(dryRun, templates.Count, 0, 0, 0, 0, 0, 0, null, [new(null, "owner_not_verified_as_teacher")]);

        if (dryRun) return await PrepareAsync(db, owner, templates, dryRun: true, ct);

        // The endpoint that publishes a single draft also takes a row lock on that draft.
        // Lock the entire set before checking its publications, so simultaneous executions can reuse them.
        var ids = templates.Keys.ToArray();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.KnowledgeCheckQuizzes.Where(quiz => quiz.OwnerSub == owner && ids.Contains(quiz.Id))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(quiz => quiz.Revision, quiz => quiz.Revision), ct);

                var batch = await PrepareAsync(db, owner, templates, dryRun: false, ct);
                if (batch.Issues.Length == 0) await transaction.CommitAsync(ct);
                return batch;
            }
            catch (Exception ex) when (attempt < 3 && IsRetryable(ex))
            {
                // A concurrent draft import can insert one of the missing stable keys.
                // The failed transaction was rolled back, so recompute the entire plan.
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds((attempt + 1) * 50), ct);
            }
        }
    }

    private static async Task<bool> IsTeacherOwnerAsync(AppDbContext db, Guid owner, CancellationToken ct)
    {
        var profileRole = await db.NotebookProfiles.Where(profile => profile.Sub == owner)
            .Select(profile => (bool?)profile.IsTeacher).SingleOrDefaultAsync(ct);
        if (profileRole is bool teacher) return teacher;

        var contactRole = await db.GiftContacts.Where(contact => contact.UserSub == owner)
            .Select(contact => (bool?)contact.IsTeacher).SingleOrDefaultAsync(ct);
        if (contactRole is bool knownTeacher) return knownTeacher;

        // Only an authenticated teacher can create a draft through the API.
        return await db.KnowledgeCheckQuizzes.AnyAsync(quiz => quiz.OwnerSub == owner, ct);
    }

    private static async Task<BatchResult> PrepareAsync(AppDbContext db, Guid owner,
        IReadOnlyDictionary<Guid, LegacyKnowledgeCheckCatalog.Template> templates, bool dryRun, CancellationToken ct)
    {
        var ids = templates.Keys.ToArray();
        var drafts = await db.KnowledgeCheckQuizzes.AsNoTracking()
            .Where(quiz => quiz.OwnerSub == owner && ids.Contains(quiz.Id)).ToDictionaryAsync(quiz => quiz.Id, ct);
        var knownIds = drafts.Keys.ToArray();
        var activeByDraft = await db.KnowledgeCheckPublications.AsNoTracking()
            .Where(row => row.IsActive && row.HoldId == null && row.MaxAttempts == null && row.FeedbackMode == AfterSubmit
                && knownIds.Contains(row.Version.QuizId))
            .Select(row => new { DraftId = row.Version.QuizId, row.Id, row.CreatedAtUtc })
            .OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).ToListAsync(ct);
        var active = activeByDraft.DistinctBy(row => row.DraftId).ToDictionary(row => row.DraftId, row => row.Id);
        var snapshots = await db.KnowledgeCheckVersions.AsNoTracking()
            .Where(version => knownIds.Contains(version.QuizId) && version.DraftRevision == version.Quiz.Revision)
            .ToDictionaryAsync(version => version.QuizId, ct);

        var issues = new List<BatchIssue>();
        var draftsToAdd = new List<KnowledgeCheckQuiz>();
        var snapshotsToAdd = new List<KnowledgeCheckVersion>();
        var publicationsToAdd = new List<KnowledgeCheckPublication>();
        var now = DateTime.UtcNow;
        foreach (var (id, template) in templates.OrderBy(item => item.Key))
        {
            if (active.ContainsKey(id)) continue;
            if (!drafts.TryGetValue(id, out var draft))
            {
                draft = new KnowledgeCheckQuiz
                {
                    Id = id, OwnerSub = owner, Title = template.Draft.Title, Description = template.Draft.Description,
                    Topic = template.Draft.Topic, EstimatedMinutes = template.Draft.EstimatedMinutes,
                    QuestionsJson = Write(template.Draft.Questions), QuestionCount = template.Draft.Questions.Length,
                    UpdatedAtUtc = now, Revision = 1
                };
                draftsToAdd.Add(draft);
            }

            if (!IsFreeExerciseReady(draft.Title, draft.Description, draft.Topic, draft.EstimatedMinutes, draft.QuestionCount, draft.QuestionsJson))
            {
                issues.Add(new(template.Key, "draft_not_ready_for_free_exercise"));
                continue;
            }

            if (!snapshots.TryGetValue(id, out var version))
            {
                version = new KnowledgeCheckVersion
                {
                    Id = Guid.NewGuid(), QuizId = draft.Id, DraftRevision = draft.Revision, Title = draft.Title,
                    Description = draft.Description, Topic = draft.Topic, EstimatedMinutes = draft.EstimatedMinutes,
                    QuestionsJson = draft.QuestionsJson, QuestionCount = draft.QuestionCount
                };
                snapshotsToAdd.Add(version);
            }
            else if (!IsFreeExerciseReady(version.Title, version.Description, version.Topic, version.EstimatedMinutes,
                version.QuestionCount, version.QuestionsJson))
            {
                issues.Add(new(template.Key, "snapshot_not_ready_for_free_exercise"));
                continue;
            }

            publicationsToAdd.Add(new()
            {
                Id = Guid.NewGuid(), VersionId = version.Id, IsActive = true, FeedbackMode = AfterSubmit, CreatedAtUtc = now
            });
        }

        var apply = !dryRun && issues.Count == 0;
        if (apply && publicationsToAdd.Count > 0)
        {
            db.KnowledgeCheckQuizzes.AddRange(draftsToAdd);
            db.KnowledgeCheckVersions.AddRange(snapshotsToAdd);
            db.KnowledgeCheckPublications.AddRange(publicationsToAdd);
            await db.SaveChangesAsync(ct);
        }

        var published = apply ? publicationsToAdd.Count : 0;
        var sample = activeByDraft.FirstOrDefault()?.Id ?? (apply ? publicationsToAdd.FirstOrDefault()?.Id : null);
        return new(dryRun, templates.Count, apply ? draftsToAdd.Count : 0, published, active.Count, active.Count + published,
            draftsToAdd.Count, publicationsToAdd.Count, sample, issues.ToArray());
    }

    private static bool IsFreeExerciseReady(string title, string description, string topic,
        int? estimatedMinutes, int count, string json)
    {
        try
        {
            var questions = Read<KnowledgeCheckQuestion>(json);
            return count > 0 && count == questions.Length
                && NormalizeDraft(new(title, description, topic, estimatedMinutes, questions), out var normalized, out _)
                && normalized.Questions.All(question => IsReady(question) && !question.ManualGrading);
        }
        catch (JsonException) { return false; }
    }

    private static bool IsRetryable(Exception ex) =>
        ex is DbUpdateException update && NotebookPeriods.IsUniqueViolation(update)
        || (ex as PostgresException ?? ex.InnerException as PostgresException) is
            { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected };

    internal sealed record BatchResult(bool DryRun, int Total, int AddedDrafts, int Published, int AlreadyActive, int FinalActive,
        int DraftsToAdd, int PublicationsToAdd, Guid? SamplePublicationId, BatchIssue[] Issues);
    internal sealed record BatchIssue(string? Source, string Code);
}
