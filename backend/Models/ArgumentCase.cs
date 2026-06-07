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
    DateTime CreatedAtUtc,
    int ChangeCount
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
    Guid SideAUserId,
    string SideAClaim,
    Guid InvitedUserId
);

public record AcceptInvitationRequest(
    Guid UserId,
    string Claim
);

public record DeclineInvitationRequest(
    Guid UserId
);

public record SendFriendRequestDto(
    Guid FromUserId,
    Guid ToUserId
);

public record RespondFriendRequestDto(
    Guid ActorUserId
);

public record RemoveFriendDto(
    Guid ActorUserId,
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
    Guid UserId,
    CaseSide Side
);

public record CloseCaseRequest(
    Guid ActorUserId
);

public record CreateCaseCommentRequest(
    Guid UserId,
    string Message
);

public record UserRewardView(
    string BadgeCode,
    string BadgeLabel,
    string IconKey,
    string Tier,
    string Reason,
    DateTime AwardedAtUtc
);
