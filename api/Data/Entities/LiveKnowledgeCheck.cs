namespace Mercantec.Tech.Api.Data.Entities;

public sealed class LiveKnowledgeCheck
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public Guid HostSub { get; set; }
    public string Title { get; set; } = "";
    public int QuestionSeconds { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public string GameJson { get; set; } = "{}";
    public DateTime ExpiresAtUtc { get; set; }
}
