namespace Mercantec.Tech.Contracts;

public sealed record HoldCalendarEventView(Guid Id, Guid HoldId, string Title, string Description,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Location, string Kind, int Version,
    string? AttendanceMode = null, Guid? QuestId = null);
public sealed record SaveHoldCalendarEvent(Guid HoldId, string Title, string Description,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Location, string Kind = "teaching", int Version = 0,
    string? AttendanceMode = null, Guid? QuestId = null);

public sealed record CopyHoldCalendarEventToOutlook(string[] TargetIds);
public sealed record OutlookCalendarCopyView(string TargetId, string Status, string? ErrorCode = null);
public sealed record OutlookCalendarCopies(OutlookCalendarCopyView[] Copies);

public sealed record HoldCalendarOverview(HoldCalendarSource[] Holds, HoldCalendarEventView[] Events,
    CalendarQuestDeadline[] Deadlines, CalendarQuestChoice[] QuestChoices);
public sealed record HoldCalendarSource(Guid HoldId, Guid ForloebId, string Name, bool CanManage);
public sealed record CalendarQuestDeadline(Guid QuestId, Guid HoldId, string Title, DateTimeOffset DeadlineUtc);
public sealed record CalendarQuestChoice(Guid QuestId, Guid HoldId, string Title);
