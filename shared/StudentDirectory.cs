namespace Mercantec.Tech.Contracts;

public sealed record StudentDirectoryResponse(
    List<StudentDirectoryStudent> Students,
    bool LectioAvailable,
    string? Warning);

public sealed record StudentDirectoryStudent(
    string Key,
    string? LectioId,
    Guid? UserSub,
    string DisplayName,
    string? ClassName,
    List<StudentDirectoryHold> Holds,
    string Placement,
    string? CurrentSchoolPeriod,
    string? WorkplaceName,
    string? LastPassedSchoolPeriod,
    bool HasTeacherOverrides,
    string? SelectedAvatarId,
    bool HasCustomPhoto,
    DateTimeOffset? UpdatedAtUtc);

public sealed record StudentDirectoryHold(Guid Id, string Name, string? ForloebName);

/// <summary>
/// Null leaves a field unchanged. Empty text explicitly clears a value.
/// ResetToLectio removes every teacher correction for this student.
/// Placement uses school, workplace, or unknown.
/// </summary>
public sealed record UpdateStudentDirectoryDetails(
    string? Placement,
    string? CurrentSchoolPeriod,
    string? WorkplaceName,
    string? LastPassedSchoolPeriod,
    bool ResetToLectio = false);
