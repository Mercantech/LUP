namespace Mercantec.Tech.Api.Data.Entities;

public sealed class HoldCalendarEvent
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public string Location { get; set; } = "";
    public string Kind { get; set; } = "teaching";
    public string? AttendanceMode { get; set; }
    public Guid? QuestId { get; set; }
    public int Version { get; set; }
}

public sealed class HoldCalendarChange
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public Guid EventId { get; set; }
    public int EventVersion { get; set; }
    public Guid? QuestId { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateTimeOffset ChangedAtUtc { get; set; }
}

public sealed class HoldCalendarOutlookCopy
{
    public Guid EventId { get; set; }
    public string Mailbox { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string? GraphEventId { get; set; }
    public string? ErrorCode { get; set; }
    public Guid? AttemptId { get; set; }
    public long LeaseExpiresUnixSeconds { get; set; }
}
