namespace Mercantec.Tech.Contracts;

public sealed record KnowledgeCheckQuestion(Guid Id, string Kind, string Prompt, string[] Options, int[] CorrectOptions,
    string[] AcceptedAnswers, bool ManualGrading, string Explanation, string? LearnMoreUrl);

public sealed record KnowledgeCheckDraft(string Title, string Description, string Topic, int? EstimatedMinutes,
    KnowledgeCheckQuestion[] Questions, long? Revision = null);

public sealed record KnowledgeCheckDraftView(Guid Id, string Title, string Description, string Topic, int? EstimatedMinutes,
    KnowledgeCheckQuestion[] Questions, DateTime UpdatedAtUtc, long Revision);

public sealed record KnowledgeCheckOwned(Guid Id, string Title, string Topic, int QuestionCount, DateTime UpdatedAtUtc);

public sealed record KnowledgeCheckHold(Guid Id, string Name);

public sealed record KnowledgeCheckPublish(Guid? HoldId, int? MaxAttempts, string FeedbackMode, long? Revision = null,
    Guid? RequestId = null);

public sealed record KnowledgeCheckPublicationView(Guid Id, Guid QuizId, string Title, string Description, string Topic,
    int? EstimatedMinutes, int QuestionCount, Guid? HoldId, string? HoldName, int? MaxAttempts, bool IsActive,
    bool CanManage, string FeedbackMode, bool FeedbackReleased, Guid? ActiveAttemptId, Guid? LatestAttemptId,
    int AttemptsUsed, DateTime CreatedAtUtc);

public sealed record KnowledgeCheckHistory(Guid PublicationId, Guid AttemptId, string Title, string Status, int? Score,
    int QuestionCount, DateTime StartedAtUtc, DateTime? SubmittedAtUtc, bool CanOpen);

public sealed record KnowledgeCheckIndex(KnowledgeCheckOwned[] Owned, KnowledgeCheckPublicationView[] Publications,
    KnowledgeCheckHistory[] History, KnowledgeCheckHold[] Holds);

public sealed record KnowledgeCheckStart(Guid? RequestId = null);

public sealed record KnowledgeCheckAnswer(Guid QuestionId, int[] SelectedOptions, string? Text);

public sealed record KnowledgeCheckSave(long Revision, int QuestionIndex, KnowledgeCheckAnswer[] Answers);

public sealed record KnowledgeCheckSubmit(long Revision);

public sealed record KnowledgeCheckLearnerQuestion(Guid Id, string Prompt, string Kind, string[] Options);

public sealed record KnowledgeCheckResult(Guid QuestionId, bool Correct, int[] CorrectOptions, string[] AcceptedAnswers,
    string Explanation, string? LearnMoreUrl, string? Comment);

public sealed record KnowledgeCheckAttemptView(Guid Id, Guid PublicationId, string Status, long Revision, int QuestionIndex,
    KnowledgeCheckLearnerQuestion[] Questions, KnowledgeCheckAnswer[] Answers, int? Score, KnowledgeCheckResult[]? Results,
    DateTime StartedAtUtc, DateTime? SubmittedAtUtc);

public sealed record KnowledgeCheckGrade(bool Correct, string? Comment);

public sealed record KnowledgeCheckParticipant(Guid AttemptId, string DisplayName, string Status, DateTime? SubmittedAtUtc);

public sealed record KnowledgeCheckQuestionReport(Guid Id, string Prompt, string Kind, string[] Options, int[] OptionCounts,
    int CorrectCount, int GradedCount, int AnsweredCount);

public sealed record KnowledgeCheckPendingGrade(Guid AttemptId, Guid QuestionId, string Prompt, string Answer, string DisplayName);

public sealed record KnowledgeCheckReport(KnowledgeCheckPublicationView Publication, KnowledgeCheckParticipant[] Participants,
    KnowledgeCheckQuestionReport[] Questions, KnowledgeCheckPendingGrade[] PendingGrades);
