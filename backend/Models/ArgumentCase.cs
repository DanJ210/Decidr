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

public enum UserRole
{
    Member,
    Moderator
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

public record ArgumentPost(
    CaseSide Side,
    Guid UserId,
    string UserName,
    string Claim,
    DateTime PostedAtUtc
);

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
    Guid InvitedUserId
);

public record AcceptInvitationRequest(
    string Claim
);

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
