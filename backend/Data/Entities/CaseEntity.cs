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
    public string? SideAMediaUrl { get; set; }
    public string? SideAThumbnailUrl { get; set; }
    public string? SideAMimeType { get; set; }
    public int? SideAWidthPixels { get; set; }
    public int? SideAHeightPixels { get; set; }
    public int? SideADurationSeconds { get; set; }
    public MediaStatus SideAMediaStatus { get; set; }
    public CaptionStatus SideACaptionStatus { get; set; }
    public TranscriptStatus SideATranscriptStatus { get; set; }
    public DateTime SideAPostedAtUtc { get; set; }

    // Side B (null until the invitation is accepted)
    public Guid? SideBUserId { get; set; }
    public string? SideBUserName { get; set; }
    public string? SideBClaim { get; set; }
    public string? SideBMediaUrl { get; set; }
    public string? SideBThumbnailUrl { get; set; }
    public string? SideBMimeType { get; set; }
    public int? SideBWidthPixels { get; set; }
    public int? SideBHeightPixels { get; set; }
    public int? SideBDurationSeconds { get; set; }
    public MediaStatus SideBMediaStatus { get; set; }
    public CaptionStatus SideBCaptionStatus { get; set; }
    public TranscriptStatus SideBTranscriptStatus { get; set; }
    public DateTime? SideBPostedAtUtc { get; set; }

    public Guid? InvitedUserId { get; set; }
    public CaseStatus Status { get; set; }
    public CaseSide? WinnerSide { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
