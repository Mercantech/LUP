namespace Mercantec.Tech.Api.Data.Entities;

public sealed class AccountBadge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserSub { get; set; }
    public string BadgeId { get; set; } = "";
    public int Rank { get; set; }
    public string Status { get; set; } = "pending";
    public Guid? HoldId { get; set; }
    public string Evidence { get; set; } = "";
    public Guid? ReviewerSub { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public bool NoticeRead { get; set; }
}
