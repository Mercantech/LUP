namespace Mercantec.Tech.Api.Data.Entities;

public sealed class SocialGameInvitation
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public SocialGameRoom Room { get; set; } = null!;
    public Guid RecipientSub { get; set; }
    public string SenderName { get; set; } = "";
    public DateTime? ReadAtUtc { get; set; }
}
