using backend.Data;
using backend.Data.Entities;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class EfCoreCourtService : ICommunityCourtService
{
    private const int MaxEvidenceItemsPerSide = 20;
    private const int MaxEvidenceTitleLength = 160;
    private const int MaxEvidenceResourceUrlLength = 2048;
    private const int MaxEvidenceMimeTypeLength = 128;
    private const long MaxEvidenceFileSizeBytes = 10 * 1024 * 1024;

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
        var caseEntities = _db.Cases
            .Where(c => c.Status != CaseStatus.Pending)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToList();

        if (caseEntities.Count == 0)
        {
            return [];
        }

        var caseIds = caseEntities.Select(c => c.Id).ToList();

        var voteCounts = _db.CaseVotes
            .Where(v => caseIds.Contains(v.CaseId))
            .GroupBy(v => new { v.CaseId, v.Side })
            .Select(g => new { g.Key.CaseId, g.Key.Side, Count = g.Count() })
            .ToList();

        var verdictByCaseId = voteCounts
            .GroupBy(x => x.CaseId)
            .ToDictionary(
                g => g.Key,
                g => new CommunityVerdict(
                    g.Where(x => x.Side == CaseSide.A).Select(x => x.Count).FirstOrDefault(),
                    g.Where(x => x.Side == CaseSide.B).Select(x => x.Count).FirstOrDefault()));

        return caseEntities.Select(e =>
        {
            var mapped = MapCase(e);
            return verdictByCaseId.TryGetValue(e.Id, out var verdict)
                ? mapped with { Verdict = verdict }
                : mapped;
        }).ToList();
    }

    public ArgumentCase? GetCase(Guid caseId, Guid? viewerUserId = null)
    {
        var entity = _db.Cases.Find(caseId);
        return entity is null ? null : MapCaseForViewer(entity, viewerUserId);
    }

    public IReadOnlyList<CaseComment> GetCaseComments(Guid caseId)
    {
        return _db.CaseComments
            .Where(c => c.CaseId == caseId)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new CaseComment(c.Id, c.CaseId, c.UserId, c.UserName, c.Message, c.CreatedAtUtc))
            .ToList();
    }

    public CaseEvidenceCollection GetCaseEvidence(Guid caseId)
    {
        var evidence = _db.CaseEvidence
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList()
            .Select(MapCaseEvidence)
            .ToList();

        return new CaseEvidenceCollection(
            evidence.Where(item => item.Side == CaseSide.A).ToList(),
            evidence.Where(item => item.Side == CaseSide.B).ToList());
    }

    public bool HasUserVoted(Guid caseId, Guid userId)
    {
        return _db.CaseVotes.AsNoTracking().Any(v => v.CaseId == caseId && v.UserId == userId);
    }

    public ArgumentCase CreateCase(Guid actorUserId, CreateCaseRequest request)
    {
        var sideAUser = _db.Users.Find(actorUserId)
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

    public (bool Success, string? Error, CaseComment? Comment) AddCaseComment(Guid caseId, Guid actorUserId, CreateCaseCommentRequest request)
    {
        var caseEntity = _db.Cases.Find(caseId);
        if (caseEntity is null)
        {
            return (false, "Case not found.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return (false, "Comment message is required.", null);
        }

        var trimmedMessage = request.Message.Trim();
        if (trimmedMessage.Length > 1024)
        {
            return (false, "Comment message cannot exceed 1024 characters.", null);
        }

        var user = _db.Users.Find(actorUserId);
        if (user is null)
        {
            return (false, "User not found.", null);
        }

        var createdAt = DateTime.UtcNow;
        var entity = new CaseCommentEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            UserId = user.Id,
            UserName = user.UserName,
            Message = trimmedMessage,
            CreatedAtUtc = createdAt
        };

        _db.CaseComments.Add(entity);
        _db.SaveChanges();

        return (true, null, new CaseComment(entity.Id, entity.CaseId, entity.UserId, entity.UserName, entity.Message, entity.CreatedAtUtc));
    }

    public (bool Success, string? Error, CaseEvidenceItem? Evidence) AddCaseEvidenceLink(Guid caseId, Guid actorUserId, AddCaseEvidenceLinkRequest request)
    {
        var validation = ValidateEvidenceWrite(caseId, actorUserId, request.Side);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return (false, "Evidence title is required.", null);
        }
        if (title.Length > MaxEvidenceTitleLength)
        {
            return (false, $"Evidence title cannot exceed {MaxEvidenceTitleLength} characters.", null);
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return (false, "Evidence URL must be a valid http or https link.", null);
        }

        var resourceUrl = uri.AbsoluteUri;
        if (resourceUrl.Length > MaxEvidenceResourceUrlLength)
        {
            return (false, $"Evidence URL cannot exceed {MaxEvidenceResourceUrlLength} characters.", null);
        }

        var entity = new CaseEvidenceEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Side = request.Side,
            AddedByUserId = validation.User!.Id,
            AddedByUserName = validation.User.UserName,
            Type = CaseEvidenceType.Link,
            Title = title,
            ResourceUrl = resourceUrl,
            MimeType = "",
            SizeBytes = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.CaseEvidence.Add(entity);
        _db.SaveChanges();

        return (true, null, MapCaseEvidence(entity));
    }

    public (bool Success, string? Error, CaseEvidenceItem? Evidence) AddCaseEvidenceFile(Guid caseId, Guid actorUserId, AddCaseEvidenceFileRequest request)
    {
        var validation = ValidateEvidenceWrite(caseId, actorUserId, request.Side);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        if (request.Type == CaseEvidenceType.Link)
        {
            return (false, "Uploaded evidence must be an image or document type.", null);
        }

        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return (false, "Evidence title is required.", null);
        }
        if (title.Length > MaxEvidenceTitleLength)
        {
            return (false, $"Evidence title cannot exceed {MaxEvidenceTitleLength} characters.", null);
        }

        var resourceUrl = request.ResourceUrl.Trim();
        if (resourceUrl.Length == 0 || resourceUrl.Length > MaxEvidenceResourceUrlLength)
        {
            return (false, $"Evidence resource URL must be between 1 and {MaxEvidenceResourceUrlLength} characters.", null);
        }

        var mimeType = request.MimeType.Trim();
        if (mimeType.Length == 0 || mimeType.Length > MaxEvidenceMimeTypeLength)
        {
            return (false, $"Evidence MIME type must be between 1 and {MaxEvidenceMimeTypeLength} characters.", null);
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > MaxEvidenceFileSizeBytes)
        {
            return (false, $"Evidence file size must be between 1 byte and {MaxEvidenceFileSizeBytes} bytes.", null);
        }

        var entity = new CaseEvidenceEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Side = request.Side,
            AddedByUserId = validation.User!.Id,
            AddedByUserName = validation.User.UserName,
            Type = request.Type,
            Title = title,
            ResourceUrl = resourceUrl,
            MimeType = mimeType,
            SizeBytes = request.SizeBytes,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.CaseEvidence.Add(entity);
        _db.SaveChanges();

        return (true, null, MapCaseEvidence(entity));
    }

    public bool RemoveCaseEvidence(Guid caseId, Guid evidenceId)
    {
        var entity = _db.CaseEvidence
            .SingleOrDefault(item => item.CaseId == caseId && item.Id == evidenceId);
        if (entity is null)
        {
            return false;
        }

        _db.CaseEvidence.Remove(entity);
        _db.SaveChanges();
        return true;
    }

    // -------------------------------------------------------------------------
    // Votes
    // -------------------------------------------------------------------------

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) CastVote(Guid caseId, Guid actorUserId, CastVoteRequest request)
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

        if (_db.Users.Find(actorUserId) is null)
        {
            return (false, "User not found.", null);
        }

        if (caseEntity.SideAUserId == actorUserId || caseEntity.SideBUserId == actorUserId)
        {
            return (false, "Case participants cannot vote on their own case.", null);
        }

        var existingVote = _db.CaseVotes.Find(caseId, actorUserId);
        if (existingVote is not null)
        {
            return (false, "You have already voted on this case.", null);
        }
        else
        {
            _db.CaseVotes.Add(new CaseVoteEntity
            {
                CaseId = caseId,
                UserId = actorUserId,
                Side = request.Side,
                CreatedAtUtc = DateTime.UtcNow,
                ChangeCount = 0
            });
            AwardReward(actorUserId, "VOTE_PARTICIPATION", "CaseVote", caseId, "Thanks for participating in community judging.");
        }

        _db.SaveChanges();

        var updated = MapCaseForViewer(caseEntity, actorUserId);
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

var alreadyAwardedVoterIds = _db.UserRewards
    .Where(r => r.BadgeCode == "VOTE_WINNER_MATCH" &&
                r.SourceType == "CaseClose" &&
                r.SourceId == caseEntity.Id &&
                matchingVoterIds.Contains(r.UserId))
    .Select(r => r.UserId)
    .ToHashSet();

foreach (var voterId in matchingVoterIds)
{
    if (alreadyAwardedVoterIds.Contains(voterId)) continue;

    _db.UserRewards.Add(new UserRewardEntity
    {
        Id = Guid.NewGuid(),
        UserId = voterId,
        BadgeCode = "VOTE_WINNER_MATCH",
        SourceType = "CaseClose",
        SourceId = caseEntity.Id,
        Reason = "Your vote matched the winning side.",
        AwardedAtUtc = DateTime.UtcNow
    });
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
    var badgeByCode = _db.RewardBadges
        .AsNoTracking()
        .ToDictionary(b => b.Code, b => new RewardBadge(b.Code, b.Label, b.IconKey, b.Tier, b.Description));

    var fallbackBadgeByCode = BadgeCatalog.ToDictionary(b => b.Code, b => b);

    return _db.UserRewards
        .AsNoTracking()
        .Where(r => r.UserId == userId)
        .OrderByDescending(r => r.AwardedAtUtc)
        .AsEnumerable()
        .Select(r =>
        {
            if (!badgeByCode.TryGetValue(r.BadgeCode, out var badge) &&
                !fallbackBadgeByCode.TryGetValue(r.BadgeCode, out badge))
            {
                return new UserRewardView(r.BadgeCode, r.BadgeCode, "", "", r.Reason, r.AwardedAtUtc);
            }

            return new UserRewardView(r.BadgeCode, badge.Label, badge.IconKey, badge.Tier, r.Reason, r.AwardedAtUtc);
        })
        .ToList();
}

    // -------------------------------------------------------------------------
    // Friend system
    // -------------------------------------------------------------------------

    public (bool Success, string? Error) SendFriendRequest(Guid actorUserId, SendFriendRequestDto dto)
    {
        if (_db.Users.Find(actorUserId) is null)
        {
            return (false, "Requesting user not found.");
        }

        if (_db.Users.Find(dto.ToUserId) is null)
        {
            return (false, "Target user not found.");
        }

        if (actorUserId == dto.ToUserId)
        {
            return (false, "You cannot send a friend request to yourself.");
        }

        var alreadyFriends = _db.FriendRequests.Any(r =>
            r.Status == FriendRequestStatus.Accepted &&
            ((r.FromUserId == actorUserId && r.ToUserId == dto.ToUserId) ||
             (r.FromUserId == dto.ToUserId && r.ToUserId == actorUserId)));

        if (alreadyFriends)
        {
            return (false, "You are already friends with this user.");
        }

        var pendingExists = _db.FriendRequests.Any(r =>
            r.Status == FriendRequestStatus.Pending &&
            ((r.FromUserId == actorUserId && r.ToUserId == dto.ToUserId) ||
             (r.FromUserId == dto.ToUserId && r.ToUserId == actorUserId)));

        if (pendingExists)
        {
            return (false, "A pending friend request already exists between these users.");
        }

        _db.FriendRequests.Add(new FriendRequestEntity
        {
            Id = Guid.NewGuid(),
            FromUserId = actorUserId,
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
            .Select(u => new AppUser(u.Id, u.UserName, u.DisplayName, u.Role))
            .ToList();
    }

    public IReadOnlyList<FriendRequest> GetFriendRequests(Guid userId)
    {
        return _db.FriendRequests
            .Where(r => r.ToUserId == userId && r.Status == FriendRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new FriendRequest(r.Id, r.FromUserId, r.ToUserId, r.Status, r.CreatedAtUtc))
            .ToList();
    }

    public IReadOnlyList<FriendRequest> GetOutgoingFriendRequests(Guid userId)
    {
        return _db.FriendRequests
            .Where(r => r.FromUserId == userId && r.Status == FriendRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new FriendRequest(r.Id, r.FromUserId, r.ToUserId, r.Status, r.CreatedAtUtc))
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
            .ToList()
            .Select(MapCase)
            .ToList();
    }

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) AcceptCaseInvitation(Guid caseId, Guid actorUserId, AcceptInvitationRequest request)
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

        if (caseEntity.InvitedUserId != actorUserId)
        {
            return (false, "You are not the invited user for this case.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Claim))
        {
            return (false, "A claim is required to accept the invitation.", null);
        }

        var sideBUser = _db.Users.Find(actorUserId);
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

    private CurrentUserVote? BuildCurrentUserVote(Guid caseId, Guid viewerUserId)
    {
        var vote = _db.CaseVotes.Find(caseId, viewerUserId);
        if (vote is null)
        {
            return null;
        }

        return new CurrentUserVote(
            vote.Side,
            vote.CreatedAtUtc,
            vote.CreatedAtUtc,
            false);
    }

    private ArgumentCase MapCaseForViewer(CaseEntity entity, Guid? viewerUserId)
    {
        var refreshed = RefreshVerdict(MapCase(entity));
        if (!viewerUserId.HasValue)
        {
            return refreshed;
        }

        return refreshed with
        {
            CurrentUserVote = BuildCurrentUserVote(entity.Id, viewerUserId.Value)
        };
    }

    private (bool Success, string? Error, UserEntity? User) ValidateEvidenceWrite(Guid caseId, Guid userId, CaseSide side)
    {
        var caseEntity = _db.Cases.Find(caseId);
        if (caseEntity is null)
        {
            return (false, "Case not found.", null);
        }

        if (caseEntity.Status != CaseStatus.Open)
        {
            return (false, "Evidence can only be added while a case is open.", null);
        }

        var user = _db.Users.Find(userId);
        if (user is null)
        {
            return (false, "User not found.", null);
        }

        Guid? sideOwnerUserId = side == CaseSide.A ? caseEntity.SideAUserId : caseEntity.SideBUserId;
        if (!sideOwnerUserId.HasValue)
        {
            return (false, "The selected side is not active on this case.", null);
        }

        if (sideOwnerUserId.Value != userId)
        {
            return (false, "Only the owner of this side can add evidence for it.", null);
        }

        var currentEvidenceCountForSide = _db.CaseEvidence.Count(item => item.CaseId == caseId && item.Side == side);
        if (currentEvidenceCountForSide >= MaxEvidenceItemsPerSide)
        {
            return (false, $"Side {side} already has the maximum of {MaxEvidenceItemsPerSide} evidence items.", null);
        }

        return (true, null, user);
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
            e.CreatedAtUtc,
            CurrentUserVote: null);
    }

    private static FriendRequest MapFriendRequest(FriendRequestEntity e) =>
        new(e.Id, e.FromUserId, e.ToUserId, e.Status, e.CreatedAtUtc);

    private static CaseEvidenceItem MapCaseEvidence(CaseEvidenceEntity entity)
    {
        return new CaseEvidenceItem(
            entity.Id,
            entity.CaseId,
            entity.Side,
            entity.AddedByUserId,
            entity.AddedByUserName,
            entity.Type,
            entity.Title,
            entity.ResourceUrl,
            entity.MimeType.Length == 0 ? null : entity.MimeType,
            entity.Type == CaseEvidenceType.Link ? null : entity.SizeBytes,
            entity.CreatedAtUtc);
    }
}
