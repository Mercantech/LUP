using System.Text.Json;
using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Api.HomeGame;

internal static class HomeGameJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException("Invalid stored home game data.");
}

public sealed class HomeGameProfileRow
{
    public Guid UserSub { get; set; }
    public int Revision { get; set; }
    public string StateJson { get; set; } = null!;
}

internal sealed record HomeProfileData(HomeLayout Layout, IReadOnlyList<HomeProgramDraft> Drafts,
    IReadOnlyList<HomeTaskProgress> Progress, string Reflection = "", HomeUndo? Undo = null)
{
    public int CompletedTasks => Enumerable.Range(1, HomeCatalog.Tasks.Count)
        .TakeWhile(task => Progress.Any(progress => progress.Task == task)).Count();
}

internal sealed record HomeUndo(HomeLayout Layout, int GoldDelta);
internal sealed record HomeStoredRun(HomeRunReplay Replay, Guid WorkerId);

public sealed class HomeBuildReceiptRow
{
    public Guid UserSub { get; set; }
    public Guid OperationId { get; set; }
    public string RequestHash { get; set; } = null!;
    public string StateJson { get; set; } = null!;
}

public sealed class HomeProgramRow
{
    public Guid Id { get; set; }
    public Guid UserSub { get; set; }
    public int Revision { get; set; }
    public string SourceJson { get; set; } = null!;
}

public sealed class HomeRunRow
{
    public Guid Id { get; set; }
    public Guid UserSub { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public bool Active { get; set; }
    public string RunJson { get; set; } = null!;
}

public sealed class HomeClassroomRow
{
    public Guid HoldId { get; set; }
    public int Revision { get; set; }
    public string SettingsJson { get; set; } = null!;
}

public sealed class HomeHelpRow
{
    public Guid Id { get; set; }
    public Guid UserSub { get; set; }
    public Guid HoldId { get; set; }
    public string RequestHash { get; set; } = null!;
    public string DataJson { get; set; } = null!;
}
