using System.Text.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Data.Entities;

public sealed class FavoriteShortcutList
{
    public Guid UserSub { get; set; }
    public Guid Revision { get; set; }
    public string ItemsJson { get; set; } = "[]";

    public IReadOnlyList<FavoriteShortcut> ReadItems() =>
        JsonSerializer.Deserialize<FavoriteShortcut[]>(ItemsJson) ?? [];
}
