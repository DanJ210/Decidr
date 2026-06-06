using backend.Models;

namespace backend.Data.Entities;

public class CaseEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Summary { get; set; } = "";

    // Side A (always present)
    public Guid SideAUserId { get; set; }
    public string SideAUserName { get; set; } = "";
    public string SideAClaim { get; set; } = "";
    public DateTime SideAPostedAtUtc { get; set; }

    // Side B (null until the invitation is accepted)
    public Guid? SideBUserId { get; set; }
    public string? SideBUserName { get; set; }
    public string? SideBClaim { get; set; }
    public DateTime? SideBPostedAtUtc { get; set; }

    public Guid? InvitedUserId { get; set; }
    public CaseStatus Status { get; set; }
    public CaseSide? WinnerSide { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
