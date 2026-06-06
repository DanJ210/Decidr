using backend.Data;
using backend.Data.Entities;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class EfCoreCourtService : ICommunityCourtService
{
    private static readonly List<RewardBadge> BadgeCatalog =
    [
        new("VOTE_PARTICIPATION", "Community Juror", "jury", "Bronze", "Awarded for participating in community voting."),
        new("VOTE_WINNER_MATCH", "Sharp Eye", "target", "Silver", "Awarded when your vote matches the winning side."),
        new("POST_PARTICIPATION", "Case Contributor", "quill", "Bronze", "Awarded for posting a side in a case."),
        new("CASE_VICTOR", "Court Victor", "crown", "Gold", "Awarded to the winning side poster when a case is closed.")
    ];

    private readonly DecidirDbContext _db;

    public EfCoreCourtService(DecidirDbContext db)
    {
        _db = db;
    }

    // -------------------------------------------------------------------------
    // Users
    // -------------------------------------------------------------------------

    public IReadOnlyList<AppUser> GetUsers()
    {
        return _db.Users
            .OrderBy(u => u.DisplayName)
            .Select(u => new AppUser(u.Id, u.UserName, u.DisplayName, u.Role))
            .ToList();
    }

    public AppUser? GetUser(Guid userId)
    {
        var entity = _db.Users.Find(userId);
        return entity is null ? null : MapUser(entity);
    }

    // -------------------------------------------------------------------------
    // Cases
    // -------------------------------------------------------------------------

    public IReadOnlyList<ArgumentCase> GetCases()
    {
        var cases = _db.Cases
            .Where(c => c.Status != CaseStatus.Pending)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToList();

        return cases.Select(c => RefreshVerdict(MapCase(c))).ToList();
    }

    public ArgumentCase? GetCase(Guid caseId)
    {
        var entity = _db.Cases.Find(caseId);
        return entity is null ? null : RefreshVerdict(MapCase(entity));
    }

    public ArgumentCase CreateCase(CreateCaseRequest request)
    {
        var sideAUser = _db.Users.Find(request.SideAUserId)
            ?? throw new InvalidOperationException("Side A user not found.");

        var createdAt = DateTime.UtcNow;
        var entity = new CaseEntity
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Category = request.Category,
            Summary = request.Summary,
            SideAUserId = sideAUser.Id,
            SideAUserName = sideAUser.UserName,
            SideAClaim = request.SideAClaim,
            SideAPostedAtUtc = createdAt,
            InvitedUserId = request.InvitedUserId,
            Status = CaseStatus.Pending,
            CreatedAtUtc = createdAt
        };

        _db.Cases.Add(entity);
        AwardReward(sideAUser.Id, "POST_PARTICIPATION", "CaseCreate", entity.Id, "Posted the Side A argument in a new case.");
        _db.SaveChanges();

        return MapCase(entity);
    }

    // -------------------------------------------------------------------------
    // Votes
    // -------------------------------------------------------------------------

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) CastVote(Guid caseId, CastVoteRequest request)
    {
        var caseEntity = _db.Cases.Find(caseId);
        if (caseEntity is null)
        {
            return (false, "Case not found.", null);
        }

        if (caseEntity.Status != CaseStatus.Open)
        {
            return (false, "Case is not open and can no longer receive votes.", null);
        }

        if (_db.Users.Find(request.UserId) is null)
        {
            return (false, "User not found.", null);
        }

        if (caseEntity.SideAUserId == request.UserId || caseEntity.SideBUserId == request.UserId)
        {
            return (false, "Case participants cannot vote on their own case.", null);
        }

        var existingVote = _db.CaseVotes.Find(caseId, request.UserId);
        if (existingVote is not null)
        {
            if (existingVote.Side == request.Side)
            {
                return (false, "You already voted for this side.", null);
            }

            if (existingVote.ChangeCount >= 1)
            {
                return (false, "You can only change your vote once.", null);
            }

            existingVote.Side = request.Side;
            existingVote.ChangeCount += 1;
        }
        else
        {
            _db.CaseVotes.Add(new CaseVoteEntity
            {
                CaseId = caseId,
                UserId = request.UserId,
                Side = request.Side,
                CreatedAtUtc = DateTime.UtcNow,
                ChangeCount = 0
            });
            AwardReward(request.UserId, "VOTE_PARTICIPATION", "CaseVote", caseId, "Thanks for participating in community judging.");
        }

        _db.SaveChanges();

        var updated = RefreshVerdict(MapCase(caseEntity));
        return (true, null, updated);
    }

    // -------------------------------------------------------------------------
    // Close case
    // -------------------------------------------------------------------------

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) CloseCase(Guid caseId, Guid actorUserId)
    {
        var actorUser = _db.Users.Find(actorUserId);
        if (actorUser is null)
        {
            return (false, "Acting user not found.", null);
        }

        var caseEntity = _db.Cases.Find(caseId);
        if (caseEntity is null)
        {
            return (false, "Case not found.", null);
        }

        if (caseEntity.Status == CaseStatus.Pending)
        {
            return (false, "Case is still pending acceptance and cannot be closed this way.", null);
        }

        var actorIsParticipant = caseEntity.SideAUserId == actorUserId || caseEntity.SideBUserId == actorUserId;
        var actorIsModerator = actorUser.Role == UserRole.Moderator;

        if (!actorIsParticipant && !actorIsModerator)
        {
            return (false, "Only case participants or moderators can close a case.", null);
        }

        if (caseEntity.Status == CaseStatus.Closed)
        {
            return (true, null, RefreshVerdict(MapCase(caseEntity)));
        }

        caseEntity.Status = CaseStatus.Closed;
        caseEntity.WinnerSide = ResolveWinnerSide(caseId);
        _db.SaveChanges();

        if (caseEntity.WinnerSide is not null)
        {
            var winnerUserId = caseEntity.WinnerSide == CaseSide.A ? caseEntity.SideAUserId : caseEntity.SideBUserId!.Value;
            AwardReward(winnerUserId, "CASE_VICTOR", "CaseClose", caseEntity.Id, "Awarded for becoming the victor of this case.");

            var matchingVoterIds = _db.CaseVotes
                .Where(v => v.CaseId == caseId && v.Side == caseEntity.WinnerSide)
                .Select(v => v.UserId)
                .Distinct()
                .ToList();

            foreach (var voterId in matchingVoterIds)
            {
                AwardReward(voterId, "VOTE_WINNER_MATCH", "CaseClose", caseEntity.Id, "Your vote matched the winning side.");
            }

            _db.SaveChanges();
        }

        return (true, null, RefreshVerdict(MapCase(caseEntity)));
    }

    // -------------------------------------------------------------------------
    // Rewards
    // -------------------------------------------------------------------------

    public IReadOnlyList<UserRewardView> GetUserRewards(Guid userId)
    {
        var badgeByCode = BadgeCatalog.ToDictionary(b => b.Code, b => b);

        return _db.UserRewards
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.AwardedAtUtc)
            .AsEnumerable()
            .Select(r =>
            {
                var badge = badgeByCode[r.BadgeCode];
                return new UserRewardView(r.BadgeCode, badge.Label, badge.IconKey, badge.Tier, r.Reason, r.AwardedAtUtc);
            })
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Friend system
    // -------------------------------------------------------------------------

    public (bool Success, string? Error) SendFriendRequest(SendFriendRequestDto dto)
    {
        if (_db.Users.Find(dto.FromUserId) is null)
        {
            return (false, "Requesting user not found.");
        }

        if (_db.Users.Find(dto.ToUserId) is null)
        {
            return (false, "Target user not found.");
        }

        if (dto.FromUserId == dto.ToUserId)
        {
            return (false, "You cannot send a friend request to yourself.");
        }

        var alreadyFriends = _db.FriendRequests.Any(r =>
            r.Status == FriendRequestStatus.Accepted &&
            ((r.FromUserId == dto.FromUserId && r.ToUserId == dto.ToUserId) ||
             (r.FromUserId == dto.ToUserId && r.ToUserId == dto.FromUserId)));

        if (alreadyFriends)
        {
            return (false, "You are already friends with this user.");
        }

        var pendingExists = _db.FriendRequests.Any(r =>
            r.Status == FriendRequestStatus.Pending &&
            ((r.FromUserId == dto.FromUserId && r.ToUserId == dto.ToUserId) ||
             (r.FromUserId == dto.ToUserId && r.ToUserId == dto.FromUserId)));

        if (pendingExists)
        {
            return (false, "A pending friend request already exists between these users.");
        }

        _db.FriendRequests.Add(new FriendRequestEntity
        {
            Id = Guid.NewGuid(),
            FromUserId = dto.FromUserId,
            ToUserId = dto.ToUserId,
            Status = FriendRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        });

        _db.SaveChanges();
        return (true, null);
    }

    public (bool Success, string? Error) RespondToFriendRequest(Guid requestId, Guid actorUserId, bool accept)
    {
        var request = _db.FriendRequests.Find(requestId);
        if (request is null)
        {
            return (false, "Friend request not found.");
        }

        if (request.ToUserId != actorUserId)
        {
            return (false, "Only the recipient can respond to this request.");
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            return (false, "This request has already been responded to.");
        }

        request.Status = accept ? FriendRequestStatus.Accepted : FriendRequestStatus.Declined;
        _db.SaveChanges();
        return (true, null);
    }

    public (bool Success, string? Error) RemoveFriend(Guid actorUserId, Guid friendUserId)
    {
        if (_db.Users.Find(actorUserId) is null)
        {
            return (false, "Acting user not found.");
        }

        if (_db.Users.Find(friendUserId) is null)
        {
            return (false, "Friend user not found.");
        }

        if (actorUserId == friendUserId)
        {
            return (false, "You cannot remove yourself as a friend.");
        }

        var friendLinks = _db.FriendRequests
            .Where(r =>
                r.Status == FriendRequestStatus.Accepted &&
                ((r.FromUserId == actorUserId && r.ToUserId == friendUserId) ||
                 (r.FromUserId == friendUserId && r.ToUserId == actorUserId)))
            .ToList();

        if (friendLinks.Count == 0)
        {
            return (false, "Users are not currently connected as friends.");
        }

        _db.FriendRequests.RemoveRange(friendLinks);
        _db.SaveChanges();
        return (true, null);
    }

    public IReadOnlyList<AppUser> GetFriends(Guid userId)
    {
        var friendIds = _db.FriendRequests
            .Where(r => r.Status == FriendRequestStatus.Accepted &&
                        (r.FromUserId == userId || r.ToUserId == userId))
            .Select(r => r.FromUserId == userId ? r.ToUserId : r.FromUserId)
            .Distinct()
            .ToHashSet();

        return _db.Users
            .Where(u => friendIds.Contains(u.Id))
            .Select(u => MapUser(u))
            .ToList();
    }

    public IReadOnlyList<FriendRequest> GetFriendRequests(Guid userId)
    {
        return _db.FriendRequests
            .Where(r => r.ToUserId == userId && r.Status == FriendRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => MapFriendRequest(r))
            .ToList();
    }

    public IReadOnlyList<FriendRequest> GetOutgoingFriendRequests(Guid userId)
    {
        return _db.FriendRequests
            .Where(r => r.FromUserId == userId && r.Status == FriendRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => MapFriendRequest(r))
            .ToList();
    }

    public bool AreFriends(Guid userId, Guid otherUserId)
    {
        return _db.FriendRequests.Any(r =>
            r.Status == FriendRequestStatus.Accepted &&
            ((r.FromUserId == userId && r.ToUserId == otherUserId) ||
             (r.FromUserId == otherUserId && r.ToUserId == userId)));
    }

    // -------------------------------------------------------------------------
    // Case invitations
    // -------------------------------------------------------------------------

    public IReadOnlyList<ArgumentCase> GetPendingInvitations(Guid userId)
    {
        return _db.Cases
            .Where(c => c.Status == CaseStatus.Pending && c.InvitedUserId == userId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => MapCase(c))
            .ToList();
    }

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) AcceptCaseInvitation(Guid caseId, AcceptInvitationRequest request)
    {
        var caseEntity = _db.Cases.Find(caseId);
        if (caseEntity is null)
        {
            return (false, "Case not found.", null);
        }

        if (caseEntity.Status != CaseStatus.Pending)
        {
            return (false, "This case is not awaiting acceptance.", null);
        }

        if (caseEntity.InvitedUserId != request.UserId)
        {
            return (false, "You are not the invited user for this case.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Claim))
        {
            return (false, "A claim is required to accept the invitation.", null);
        }

        var sideBUser = _db.Users.Find(request.UserId);
        if (sideBUser is null)
        {
            return (false, "User not found.", null);
        }

        var acceptedAt = DateTime.UtcNow;
        caseEntity.SideBUserId = sideBUser.Id;
        caseEntity.SideBUserName = sideBUser.UserName;
        caseEntity.SideBClaim = request.Claim;
        caseEntity.SideBPostedAtUtc = acceptedAt;
        caseEntity.Status = CaseStatus.Open;
        caseEntity.InvitedUserId = null;

        AwardReward(sideBUser.Id, "POST_PARTICIPATION", "CaseCreate", caseEntity.Id, "Posted the Side B argument in a new case.");
        _db.SaveChanges();

        return (true, null, MapCase(caseEntity));
    }

    public (bool Success, string? Error) DeclineCaseInvitation(Guid caseId, Guid actorUserId)
    {
        var caseEntity = _db.Cases.Find(caseId);
        if (caseEntity is null)
        {
            return (false, "Case not found.");
        }

        if (caseEntity.Status != CaseStatus.Pending)
        {
            return (false, "This case is not awaiting acceptance.");
        }

        if (caseEntity.InvitedUserId != actorUserId)
        {
            return (false, "You are not the invited user for this case.");
        }

        caseEntity.Status = CaseStatus.Closed;
        caseEntity.InvitedUserId = null;
        _db.SaveChanges();

        return (true, null);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private ArgumentCase RefreshVerdict(ArgumentCase argumentCase)
    {
        var sideAVotes = _db.CaseVotes.Count(v => v.CaseId == argumentCase.Id && v.Side == CaseSide.A);
        var sideBVotes = _db.CaseVotes.Count(v => v.CaseId == argumentCase.Id && v.Side == CaseSide.B);
        return argumentCase with { Verdict = new CommunityVerdict(sideAVotes, sideBVotes) };
    }

    private CaseSide? ResolveWinnerSide(Guid caseId)
    {
        var sideAVotes = _db.CaseVotes.Count(v => v.CaseId == caseId && v.Side == CaseSide.A);
        var sideBVotes = _db.CaseVotes.Count(v => v.CaseId == caseId && v.Side == CaseSide.B);

        if (sideAVotes > sideBVotes) return CaseSide.A;
        if (sideBVotes > sideAVotes) return CaseSide.B;
        return null;
    }

    private void AwardReward(Guid userId, string badgeCode, string sourceType, Guid sourceId, string reason)
    {
        var alreadyAwarded = _db.UserRewards.Any(r =>
            r.UserId == userId &&
            r.BadgeCode == badgeCode &&
            r.SourceType == sourceType &&
            r.SourceId == sourceId);

        if (alreadyAwarded) return;

        _db.UserRewards.Add(new UserRewardEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BadgeCode = badgeCode,
            SourceType = sourceType,
            SourceId = sourceId,
            Reason = reason,
            AwardedAtUtc = DateTime.UtcNow
        });
    }

    // -------------------------------------------------------------------------
    // Mapping helpers
    // -------------------------------------------------------------------------

    private static AppUser MapUser(UserEntity e) =>
        new(e.Id, e.UserName, e.DisplayName, e.Role);

    private static ArgumentCase MapCase(CaseEntity e)
    {
        var sideA = new ArgumentPost(CaseSide.A, e.SideAUserId, e.SideAUserName, e.SideAClaim, e.SideAPostedAtUtc);

        ArgumentPost? sideB = e.SideBUserId is not null
            ? new ArgumentPost(CaseSide.B, e.SideBUserId.Value, e.SideBUserName!, e.SideBClaim!, e.SideBPostedAtUtc!.Value)
            : null;

        return new ArgumentCase(
            e.Id,
            e.Title,
            e.Category,
            e.Summary,
            sideA,
            sideB,
            e.InvitedUserId,
            new CommunityVerdict(0, 0), // votes are refreshed by caller if needed
            e.Status,
            e.WinnerSide,
            e.CreatedAtUtc);
    }

    private static FriendRequest MapFriendRequest(FriendRequestEntity e) =>
        new(e.Id, e.FromUserId, e.ToUserId, e.Status, e.CreatedAtUtc);
}
