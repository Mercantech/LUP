namespace Mercantec.Tech.Api.Data.Entities;

public sealed class SocialGameRoom
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public Guid HostSub { get; set; }
    public bool IsStarted { get; set; }
    public string StateJson { get; set; } = "";
    public long Revision { get; set; }
    public DateTime? NextDeadlineUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
