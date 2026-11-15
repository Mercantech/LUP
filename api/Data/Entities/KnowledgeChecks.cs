namespace Mercantec.Tech.Api.Data.Entities;

public sealed class KnowledgeCheckQuiz
{
    public Guid Id { get; set; }
    public Guid OwnerSub { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Topic { get; set; } = "";
    public int? EstimatedMinutes { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public int QuestionCount { get; set; }
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class KnowledgeCheckVersion
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public KnowledgeCheckQuiz Quiz { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Topic { get; set; } = "";
    public int? EstimatedMinutes { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public int QuestionCount { get; set; }
    public long DraftRevision { get; set; }
}

public sealed class KnowledgeCheckPublication
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public Guid? RequestId { get; set; }
    public KnowledgeCheckVersion Version { get; set; } = null!;
    public Guid? HoldId { get; set; }
    public Hold? Hold { get; set; }
    public int? MaxAttempts { get; set; }
    public string FeedbackMode { get; set; } = "after-submit";
    public bool FeedbackReleased { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class KnowledgeCheckAttempt
{
    public Guid Id { get; set; }
    public Guid PublicationId { get; set; }
    public KnowledgeCheckPublication Publication { get; set; } = null!;
    public Guid UserSub { get; set; }
    public Guid? RequestId { get; set; }
    public string Status { get; set; } = "started";
    public long Revision { get; set; }
    public int QuestionIndex { get; set; }
    public string AnswersJson { get; set; } = "[]";
    public string GradesJson { get; set; } = "[]";
    public int? Score { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
}
