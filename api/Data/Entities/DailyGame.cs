using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Data.Entities;

public sealed class DailyGameSchedule
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
}

public sealed class DailyGamePuzzle
{
    public string Game { get; set; } = "";
    public DateOnly Date { get; set; }
    public string DataJson { get; set; } = "";
}

public sealed class DailyGamePlay
{
    public string Game { get; set; } = "";
    public Guid UserSub { get; set; }
    public DateOnly Date { get; set; }
    public int Version { get; set; }
    public DailyGameStatus Status { get; set; }
    public string StateJson { get; set; } = "";
}
