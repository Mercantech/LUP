namespace Mercantec.Tech.Api.Data.Entities;

public sealed class QuestGoldAward
{
    public Guid QuestId { get; set; }
    public Guid UserSub { get; set; }
    public Guid FeedbackId { get; set; }
    public int Gold { get; set; }
    public DateTimeOffset AwardedAtUtc { get; set; }
}
