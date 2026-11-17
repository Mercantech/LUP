namespace Mercantec.Tech.Api.Data.Entities;

public sealed class LiveCourseModule
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public string BlockId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? PollType { get; set; }
    public string Prompt { get; set; } = "";
    public string OptionsJson { get; set; } = "[]";
    public int? CorrectChoice { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class LiveCourseSession
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public Guid HoldId { get; set; }
    public Guid? CourseId { get; set; }
    public long PublishedRevision { get; set; }
    public long PublicationVersion { get; set; }
    public int SlideCount { get; set; }
    public int SlideIndex { get; set; }
    public string BindingsJson { get; set; } = "[]";
    public DateTime StartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public Guid? ActiveRoundId { get; set; }
    public long Revision { get; set; }
    public bool RankingVisible { get; set; }
}

public sealed class LiveCourseRound
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ModuleId { get; set; }
    public LiveCourseModule Module { get; set; } = null!;
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public bool ShowResults { get; set; }
}

public sealed class LiveCourseEntry
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Guid UserSub { get; set; }
    public int Slot { get; set; }
    public string? Text { get; set; }
    public int? Choice { get; set; }
    public bool? Approved { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class LiveCourseVote
{
    public Guid EntryId { get; set; }
    public Guid UserSub { get; set; }
}

public sealed class LiveCourseReactionRecord
{
    public Guid SessionId { get; set; }
    public Guid UserSub { get; set; }
    public string Emoji { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}
