namespace backend.Data.Entities;

public class UserRewardEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BadgeCode { get; set; } = "";
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime AwardedAtUtc { get; set; }
}
