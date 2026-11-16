namespace Mercantec.Tech.Contracts.HomeGame;

public static class HomeGameLanguages
{
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly<string>(["python", "csharp", "arduino"]);
    public static bool IsKnown(string? language) => language is not null && All.Contains(language);
}

public static class HomeGameVersion
{
    public const string Simulator = "hjem-v1";
    public const string Sdk = "virtual-home-v1";
    public const string Content = "hjem-pilot-v1";
    public const int MaxSourceBytes = 128 * 1024;
}

public sealed record HomeGameState(
    HomeLayout Layout,
    int Gold,
    int CompletedTasks,
    IReadOnlyList<HomeProgramDraft> Drafts,
    IReadOnlyList<HomeTaskProgress> Progress,
    bool CanUndo,
    HomeRunSummary? LastRun,
    bool RunnerAvailable,
    int HighestAvailableTask,
    string Reflection,
    IReadOnlyList<HomeClassroom> Classrooms,
    bool IsTeacherSandbox = false);

public sealed record HomeClassroom(Guid HoldId, string Name, int OpenThroughTask, int SuggestedTask,
    string? SuggestedLanguage, IReadOnlyDictionary<string, string> StarterCode);
public sealed record HomeProgramDraft(string Language, string Source, Guid VersionId, string Hash);
public sealed record HomeTaskProgress(int Task, string RewardKey, string ContentVersion,
    Guid RunId, DateTimeOffset CompletedAtUtc);
public sealed record HomeBuildRequest(Guid OperationId, int Revision, HomeBuildAction Action);
public sealed record HomeSaveProgramRequest(int Revision, string Language, string Source);
public sealed record HomeReflectionRequest(int Revision, string Text);
public sealed record HomeHelpRequest(Guid OperationId, Guid HoldId, int Task, string Question);
public sealed record HomeHelpEntry(Guid Id, Guid StudentSub, int Task, string Question,
    HomeRunSummary? Run, DateTimeOffset CreatedAtUtc);
public sealed record HomeTeacherSettings(int Revision, int OpenThroughTask, int SuggestedTask,
    string? SuggestedLanguage, IReadOnlyDictionary<string, string> StarterCode);
public sealed record HomeTeacherStudent(Guid StudentSub, string Name,
    IReadOnlyList<HomeTaskProgress> Progress, HomeRunSummary? LastRun, string Reflection);
public sealed record HomeTeacherOverview(Guid HoldId, string Name, HomeTeacherSettings Settings,
    IReadOnlyList<HomeTeacherStudent> Students, IReadOnlyList<HomeHelpEntry> HelpRequests);
public sealed record HomeTeacherStudentDetail(HomeTeacherStudent Student, IReadOnlyList<HomeProgramDraft> Drafts,
    HomeProgramDraft? RunProgram, HomeRunStatus? LastRun);

public sealed record HomeStartRunRequest(int Revision, string Language, int Task, string Intent = "read",
    bool Assessment = true, string? ScenarioId = null, int Speed = 1, Guid? RestartRunId = null);
public sealed record HomeStepRunRequest(long AfterSequence, int MaxTicks = 1);
public sealed record HomeRunControlRequest(string Action, string? Intent = null, int? DaylightLux = null,
    bool? AutoEnabled = null, int? Speed = null);
public sealed record HomeRunSummary(Guid Id, int Revision, Guid ProgramVersionId, string Language,
    int Task, bool Assessment, string Status, bool? Passed, string? ErrorCode,
    DateTimeOffset StartedAtUtc, string ProgramHash, string SdkVersion, string SimulatorVersion,
    string ContentVersion, bool IsTeacherSandbox = false);
public sealed record HomeRunStatus(HomeRunSummary Run, long Sequence, int ScenarioIndex,
    IReadOnlyList<string> ScenarioIds, int Speed, HomeWorldSnapshot? Snapshot,
    IReadOnlyList<HomeScenarioOutcome> Outcomes, IReadOnlyList<string> Logs,
    string? ErrorMessage, int GoldAwarded);
public sealed record HomeReplayEvent(int ScenarioIndex, int Tick, IReadOnlyList<HomeCommand> Commands,
    double? WaitMs, bool ControllerCompleted, string? Intent = null, int? DaylightLux = null,
    bool? AutoEnabled = null, HomeRuleError? Error = null);
public sealed record HomeRunReplay(HomeRunStatus Run, HomeLayout Layout, IReadOnlyList<HomeScenario> Scenarios,
    IReadOnlyList<HomeReplayEvent> Events, string InitialIntent);
