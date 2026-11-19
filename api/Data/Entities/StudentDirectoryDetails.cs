namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Teacher supplements, identified independently of a particular hold membership.</summary>
public sealed class StudentDirectoryDetails
{
    public string Key { get; set; } = "";
    public string? LectioId { get; set; }
    public Guid? UserSub { get; set; }
    public string? Placement { get; set; }
    public string? CurrentSchoolPeriod { get; set; }
    public string? WorkplaceName { get; set; }
    public string? LastPassedSchoolPeriod { get; set; }
    public Guid UpdatedBySub { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
