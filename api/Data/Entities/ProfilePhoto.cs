namespace Mercantec.Tech.Api.Data.Entities;

public sealed class ProfilePhoto
{
    public Guid UserSub { get; set; }
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "image/jpeg";
    public long SizeBytes { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
