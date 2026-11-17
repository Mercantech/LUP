namespace Mercantec.Tech.Contracts;

public sealed record SaveLiveCourseModule(string BlockId, string Kind, string Prompt, string[]? Options = null,
    int? CorrectChoice = null, int? DurationSeconds = null, string? PollType = null);
public sealed record LiveCourseModuleView(Guid Id, string BlockId, string Kind, string Prompt, string[] Options,
    int? CorrectChoice, int? DurationSeconds, string? PollType);
public sealed record StartLiveCourse(long PublishedRevision, Guid? CourseId = null);
public sealed record LiveCourseSlide(int SlideIndex);
public sealed record LiveCourseReaction(string Emoji);
public sealed record OpenLiveCourseRound(Guid ModuleId, string BlockId);
public sealed record ControlLiveCourseRound(string Action);
public sealed record AnswerLiveCourseRound(int? Choice, string? Text);
public sealed record ModerateLiveCourseEntry(bool Approved);
public sealed record VoteLiveCourseEntry(bool Voted);
public sealed record ShowLiveCourseRanking(bool Visible);

public sealed record LiveCourseView(LiveCourseSessionView? Session);
public sealed record LiveCourseSessionView(Guid Id, long PublishedRevision, int SlideIndex, long Revision, bool MayManage,
    LiveCourseReactionCount[] Reactions, LiveCourseRoundView? Round, bool RankingVisible, LiveCourseRank[] Ranking);
public sealed record LiveCourseReactionCount(string Emoji, int Count);
public sealed record LiveCourseRank(string Label, int Score);
public sealed record LiveCourseRoundView(Guid Id, Guid ModuleId, string BlockId, string Kind, string Prompt, string[] Options,
    bool Open, bool ShowResults, DateTimeOffset? DeadlineUtc, bool HasAnswered, int? YourChoice, string? YourText,
    int[]? Counts, int? CorrectChoice, LiveCourseEntryView[] Entries, int? TotalAnswers, string? PollType);
public sealed record LiveCourseEntryView(Guid Id, string Text, bool Approved, int Votes, bool YourVote, bool IsMine,
    bool Moderated);
public sealed record LiveCourseHistory(Guid Id, DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc,
    long PublishedRevision, int ParticipantCount, LiveCourseRoundSummary[] Rounds);
public sealed record LiveCourseRoundSummary(Guid Id, string BlockId, string Kind, string Prompt, string[] Options,
    int TotalAnswers, int[]? Counts, string? PollType);
