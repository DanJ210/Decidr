namespace backend.Models;

public enum CaseSide
{
    A,
    B
}

public enum CaseStatus
{
    Open,
    Closed
}

public enum UserRole
{
    Member,
    Moderator
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
    ArgumentPost SideB,
    CommunityVerdict Verdict,
    CaseStatus Status,
    CaseSide? WinnerSide,
    DateTime CreatedAtUtc
);

public record CreateCaseRequest(
    string Title,
    string Category,
    string Summary,
    Guid SideAUserId,
    string SideAClaim,
    Guid SideBUserId,
    string SideBClaim
);

public record CastVoteRequest(
    Guid UserId,
    CaseSide Side
);

public record CloseCaseRequest(
    Guid ActorUserId
);

public record UserRewardView(
    string BadgeCode,
    string BadgeLabel,
    string IconKey,
    string Tier,
    string Reason,
    DateTime AwardedAtUtc
);
