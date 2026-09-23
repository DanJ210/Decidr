namespace backend.Models;

public enum CaseSide
{
    A,
    B
}

public enum CaseStatus
{
    Pending,
    Open,
    Closed
}

public enum CaseEvidenceType
{
    Link,
    Image,
    Document
}

public enum EvidenceContentStatus
{
    Clean,
    PendingScan,
    Malicious,
    ScanFailed,
    NotFound
}

public enum UserRole
{
    Member,
    Moderator
}

public enum MediaStatus
{
    None,
    Pending,
    Ready,
    Failed,
    Uploading,
    Processing,
    Rejected
}

public enum CaptionStatus
{
    None,
    Pending,
    Ready,
    Failed
}

public enum TranscriptStatus
{
    None,
    Pending,
    Ready,
    Failed
}

public enum FriendRequestStatus
{
    Pending,
    Accepted,
    Declined
}

public record AppUser(
    Guid Id,
    string UserName,
    string DisplayName,
    UserRole Role
);

public record PlayerRecord(
    Guid UserId,
    string UserName,
    string DisplayName,
    int Wins,
    int Losses,
    int Ties,
    int CompletedCases,
    double WinRate,
    bool IsQualified,
    int? Rank
);

public record ArgumentPost(
    CaseSide Side,
    Guid UserId,
    string UserName,
    string Claim,
    DateTime PostedAtUtc)
{
    public string? MediaUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? MimeType { get; init; }
    public int? WidthPixels { get; init; }
    public int? HeightPixels { get; init; }
    public int? DurationSeconds { get; init; }
    public MediaStatus MediaStatus { get; init; } = MediaStatus.None;
    public CaptionStatus CaptionStatus { get; init; } = CaptionStatus.None;
    public TranscriptStatus TranscriptStatus { get; init; } = TranscriptStatus.None;
}

public record CommunityVerdict(
    int VotesForSideA,
    int VotesForSideB
);

public record CaseVote(
    Guid CaseId,
    Guid UserId,
    CaseSide Side,
    DateTime CreatedAtUtc
);

public record CaseVoteStatus(
    bool HasVoted
);

public record CaseFeedPage(
    IReadOnlyList<ArgumentCase> Items,
    string? NextCursor,
    bool HasMore
);

public record ReportCaseRequest(
    string Reason
);

public record ModerateCaseRequest(
    bool Hidden
);

public record PlaybackEventRequest(
    CaseSide Side,
    string Event,
    int PositionSeconds
);

public record CurrentUserVote(
    CaseSide Side,
    DateTime CastAtUtc,
    DateTime ChangeLockedAtUtc,
    bool CanChange
);

public record CaseComment(
    Guid Id,
    Guid CaseId,
    Guid UserId,
    string UserName,
    string Message,
    DateTime CreatedAtUtc
);

public record CaseEvidenceItem(
    Guid Id,
    Guid CaseId,
    CaseSide Side,
    Guid AddedByUserId,
    string AddedByUserName,
    CaseEvidenceType Type,
    string Title,
    string ResourceUrl,
    string? MimeType,
    long? SizeBytes,
    DateTime CreatedAtUtc
);

public record CaseEvidenceCollection(
    IReadOnlyList<CaseEvidenceItem> SideA,
    IReadOnlyList<CaseEvidenceItem> SideB
);

public record CaseEvidenceStatusResponse(
    EvidenceContentStatus Status
);

public record CaseMediaUploadResponse(
    string Url,
    int DurationSeconds,
    long SizeBytes,
    string ContentType
);

public record RewardBadge(
    string Code,
    string Label,
    string IconKey,
    string Tier,
    string Description
);

public record UserReward(
    Guid UserId,
    string BadgeCode,
    string SourceType,
    Guid SourceId,
    string Reason,
    DateTime AwardedAtUtc
);

public record ArgumentCase(
    Guid Id,
    string Title,
    string Category,
    string Summary,
    ArgumentPost SideA,
    ArgumentPost? SideB,
    Guid? InvitedUserId,
    CommunityVerdict Verdict,
    CaseStatus Status,
    CaseSide? WinnerSide,
    DateTime CreatedAtUtc,
    CurrentUserVote? CurrentUserVote
);

public record CreateCaseRequest(
    string Title,
    string Category,
    string Summary,
    string SideAClaim,
    Guid InvitedUserId)
{
    public string? SideARecordUrl { get; init; }
    public string? SideAThumbnailUrl { get; init; }
    public string? SideAMimeType { get; init; }
    public int? SideAWidthPixels { get; init; }
    public int? SideAHeightPixels { get; init; }
    public int? SideADurationSeconds { get; init; }
    public MediaStatus SideAMediaStatus { get; init; } = MediaStatus.None;
    public CaptionStatus SideACaptionStatus { get; init; } = CaptionStatus.None;
    public TranscriptStatus SideATranscriptStatus { get; init; } = TranscriptStatus.None;
}

public record AcceptInvitationRequest(string Claim)
{
    public string? SideBRecordUrl { get; init; }
    public string? SideBThumbnailUrl { get; init; }
    public string? SideBMimeType { get; init; }
    public int? SideBWidthPixels { get; init; }
    public int? SideBHeightPixels { get; init; }
    public int? SideBDurationSeconds { get; init; }
    public MediaStatus SideBMediaStatus { get; init; } = MediaStatus.None;
    public CaptionStatus SideBCaptionStatus { get; init; } = CaptionStatus.None;
    public TranscriptStatus SideBTranscriptStatus { get; init; } = TranscriptStatus.None;
}

public record SendFriendRequestDto(
    Guid ToUserId
);

public record RemoveFriendDto(
    Guid FriendUserId
);

public record FriendRequest(
    Guid Id,
    Guid FromUserId,
    Guid ToUserId,
    FriendRequestStatus Status,
    DateTime CreatedAtUtc
);

public record CastVoteRequest(
    CaseSide Side
);

public record CloseCaseRequest;

public record CreateCaseCommentRequest(
    string Message
);

public record AddCaseEvidenceLinkRequest(
    CaseSide Side,
    string Title,
    string Url
);

public record AddCaseEvidenceFileRequest(
    CaseSide Side,
    CaseEvidenceType Type,
    string Title,
    string ResourceUrl,
    string MimeType,
    long SizeBytes
);

public record UserRewardView(
    string BadgeCode,
    string BadgeLabel,
    string IconKey,
    string Tier,
    string Reason,
    DateTime AwardedAtUtc
);
