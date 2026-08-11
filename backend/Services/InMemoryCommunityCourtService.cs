using backend.Models;

namespace backend.Services;

public class InMemoryCommunityCourtService : ICommunityCourtService
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

    private static readonly IReadOnlyDictionary<string, RewardBadge> BadgeCatalogByCode =
        BadgeCatalog.ToDictionary(badge => badge.Code);

    private readonly object _syncRoot = new();
    private readonly List<AppUser> _users;
    private readonly List<ArgumentCase> _cases;
    private readonly List<CaseVote> _votes;
    private readonly List<CaseComment> _comments;
    private readonly List<CaseEvidenceItem> _caseEvidence;
    private readonly List<UserReward> _rewards;
    private readonly List<FriendRequest> _friendRequests;

    public InMemoryCommunityCourtService()
    {
        _users =
        [
            new(Guid.Parse("89f651a2-d6ad-43b6-a2d8-209da7599387"), "alex_t", "Alex", UserRole.Member),
            new(Guid.Parse("03a431ca-7354-43b8-b8f3-cf95f65f83b4"), "jordan_r", "Jordan", UserRole.Member),
            new(Guid.Parse("c421252a-2976-4f97-9fbf-e9f848f066f8"), "casey_l", "Casey", UserRole.Member),
            new(Guid.Parse("8af01b3a-d4b4-4954-9805-6dc58a2f0e0c"), "morgan_p", "Morgan", UserRole.Member),
            new(Guid.Parse("e1d2e6fb-c79f-4d18-8dd9-c9507487e2c4"), "sam_k", "Sam", UserRole.Moderator)
        ];

        var firstCaseId = Guid.Parse("2fd6fa9e-8ed5-4ea3-b0ef-e42fdf47c2f1");
        var secondCaseId = Guid.Parse("af1ea95c-9f7f-4cd5-b948-3c2f12c31f74");

        var alex = _users.Single(u => u.UserName == "alex_t");
        var jordan = _users.Single(u => u.UserName == "jordan_r");
        var casey = _users.Single(u => u.UserName == "casey_l");
        var morgan = _users.Single(u => u.UserName == "morgan_p");

        _cases =
        [
            BuildCase(
                firstCaseId,
                "Was cancelling 20 minutes before dinner justified?",
                "Relationships",
                "One side says a last-minute work escalation made attendance impossible. The other says there was enough notice to communicate sooner.",
                new ArgumentPost(CaseSide.A, alex.Id, alex.UserName, "I had an urgent production incident and couldn't step away without risking customer downtime.", DateTime.UtcNow.AddHours(-16)),
                new ArgumentPost(CaseSide.B, jordan.Id, jordan.UserName, "I started cooking hours earlier and only learned the cancellation at the last moment.", DateTime.UtcNow.AddHours(-15)),
                DateTime.UtcNow.AddDays(-1)),
            BuildCase(
                secondCaseId,
                "Should roommates split utility overuse charges?",
                "Roommates",
                "Heating bill spiked. Side A wants a proportional split based on room heater usage. Side B wants an equal split.",
                new ArgumentPost(CaseSide.A, casey.Id, casey.UserName, "The meter smart plugs show one room used triple the power, so the extra should not be shared equally.", DateTime.UtcNow.AddHours(-30)),
                new ArgumentPost(CaseSide.B, morgan.Id, morgan.UserName, "We agreed to split utilities evenly from day one, and changing that retroactively is unfair.", DateTime.UtcNow.AddHours(-28)),
                DateTime.UtcNow.AddDays(-2))
        ];

        _votes = [];
        _comments =
        [
            new(Guid.NewGuid(), firstCaseId, casey.Id, casey.UserName, "Both sides have a point, but timing and communication matter most here.", DateTime.UtcNow.AddHours(-10)),
            new(Guid.NewGuid(), firstCaseId, morgan.Id, morgan.UserName, "If it was truly urgent, a quick heads-up earlier would have helped.", DateTime.UtcNow.AddHours(-9)),
            new(Guid.NewGuid(), secondCaseId, alex.Id, alex.UserName, "Smart plug data feels like fair evidence for a proportional split.", DateTime.UtcNow.AddHours(-20))
        ];
        _caseEvidence =
        [
            new(
                Guid.NewGuid(),
                firstCaseId,
                CaseSide.A,
                alex.Id,
                alex.UserName,
                CaseEvidenceType.Link,
                "Incident postmortem timeline",
                "https://example.com/postmortem-timeline",
                null,
                null,
                DateTime.UtcNow.AddHours(-14)),
            new(
                Guid.NewGuid(),
                firstCaseId,
                CaseSide.B,
                jordan.Id,
                jordan.UserName,
                CaseEvidenceType.Link,
                "Dinner prep receipts and timeline",
                "https://example.com/dinner-receipts",
                null,
                null,
                DateTime.UtcNow.AddHours(-13)),
            new(
                Guid.NewGuid(),
                secondCaseId,
                CaseSide.A,
                casey.Id,
                casey.UserName,
                CaseEvidenceType.Link,
                "Smart plug usage report",
                "https://example.com/smart-plug-report",
                null,
                null,
                DateTime.UtcNow.AddHours(-27))
        ];

        _rewards = [];
        AwardCasePostingRewards(_cases[0]);
        AwardCasePostingRewards(_cases[1]);

        _friendRequests = [];
    }

    public IReadOnlyList<AppUser> GetUsers()
    {
        lock (_syncRoot)
        {
            return _users.ToList();
        }
    }

    public AppUser? GetUser(Guid userId)
    {
        lock (_syncRoot)
        {
            return _users.FirstOrDefault(u => u.Id == userId);
        }
    }

    public IReadOnlyList<PlayerRecord> GetPlayerRecords()
    {
        lock (_syncRoot)
        {
            return PlayerRecordCalculator.Calculate(_users, _cases);
        }
    }

    public PlayerRecord? GetPlayerRecord(Guid userId)
    {
        lock (_syncRoot)
        {
            return PlayerRecordCalculator.Calculate(_users, _cases)
                .FirstOrDefault(record => record.UserId == userId);
        }
    }

    public IReadOnlyList<ArgumentCase> GetCases()
    {
        lock (_syncRoot)
        {
            return _cases
                .Where(c => c.Status != CaseStatus.Pending)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Select(RefreshVerdict)
                .ToList();
        }
    }

    public ArgumentCase? GetCase(Guid caseId, Guid? viewerUserId = null)
    {
        lock (_syncRoot)
        {
            var found = _cases.FirstOrDefault(c => c.Id == caseId);
            return found is null ? null : MapCaseForViewer(found, viewerUserId);
        }
    }

    public IReadOnlyList<CaseComment> GetCaseComments(Guid caseId)
    {
        lock (_syncRoot)
        {
            return _comments
                .Where(c => c.CaseId == caseId)
                .OrderBy(c => c.CreatedAtUtc)
                .ToList();
        }
    }

    public CaseEvidenceCollection GetCaseEvidence(Guid caseId)
    {
        lock (_syncRoot)
        {
            return BuildCaseEvidenceCollection(caseId);
        }
    }

    public bool HasUserVoted(Guid caseId, Guid userId)
    {
        lock (_syncRoot)
        {
            return _votes.Any(v => v.CaseId == caseId && v.UserId == userId);
        }
    }

    public ArgumentCase CreateCase(Guid actorUserId, CreateCaseRequest request)
    {
        lock (_syncRoot)
        {
            var sideAUser = _users.First(u => u.Id == actorUserId);
            var createdAt = DateTime.UtcNow;

            var created = new ArgumentCase(
                Guid.NewGuid(),
                request.Title,
                request.Category,
                request.Summary,
                new ArgumentPost(CaseSide.A, sideAUser.Id, sideAUser.UserName, request.SideAClaim, createdAt),
                SideB: null,
                InvitedUserId: request.InvitedUserId,
                new CommunityVerdict(0, 0),
                CaseStatus.Pending,
                WinnerSide: null,
                createdAt,
                CurrentUserVote: null);

            _cases.Add(created);
            AwardReward(sideAUser.Id, "POST_PARTICIPATION", "CaseCreate", created.Id, "Posted the Side A argument in a new case.");
            return created;
        }
    }

    public (bool Success, string? Error, CaseComment? Comment) AddCaseComment(Guid caseId, Guid actorUserId, CreateCaseCommentRequest request)
    {
        lock (_syncRoot)
        {
            if (_cases.All(c => c.Id != caseId))
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

            var user = _users.FirstOrDefault(u => u.Id == actorUserId);
            if (user is null)
            {
                return (false, "User not found.", null);
            }

            var created = new CaseComment(
                Guid.NewGuid(),
                caseId,
                user.Id,
                user.UserName,
                trimmedMessage,
                DateTime.UtcNow);

            _comments.Add(created);
            return (true, null, created);
        }
    }

    public (bool Success, string? Error, CaseEvidenceItem? Evidence) AddCaseEvidenceLink(Guid caseId, Guid actorUserId, AddCaseEvidenceLinkRequest request)
    {
        lock (_syncRoot)
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

            var created = new CaseEvidenceItem(
                Guid.NewGuid(),
                caseId,
                request.Side,
                validation.User!.Id,
                validation.User.UserName,
                CaseEvidenceType.Link,
                title,
                resourceUrl,
                null,
                null,
                DateTime.UtcNow);

            _caseEvidence.Add(created);
            return (true, null, created);
        }
    }

    public (bool Success, string? Error, CaseEvidenceItem? Evidence) AddCaseEvidenceFile(Guid caseId, Guid actorUserId, AddCaseEvidenceFileRequest request)
    {
        lock (_syncRoot)
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

            var created = new CaseEvidenceItem(
                Guid.NewGuid(),
                caseId,
                request.Side,
                validation.User!.Id,
                validation.User.UserName,
                request.Type,
                title,
                resourceUrl,
                mimeType,
                request.SizeBytes,
                DateTime.UtcNow);

            _caseEvidence.Add(created);
            return (true, null, created);
        }
    }

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) CastVote(Guid caseId, Guid actorUserId, CastVoteRequest request)
    {
        lock (_syncRoot)
        {
            var foundCase = _cases.FirstOrDefault(c => c.Id == caseId);
            if (foundCase is null)
            {
                return (false, "Case not found.", null);
            }

            if (foundCase.Status != CaseStatus.Open)
            {
                return (false, "Case is not open and can no longer receive votes.", null);
            }

            if (_users.All(u => u.Id != actorUserId))
            {
                return (false, "User not found.", null);
            }

            if (foundCase.SideA.UserId == actorUserId || foundCase.SideB?.UserId == actorUserId)
            {
                return (false, "Case participants cannot vote on their own case.", null);
            }

            var existingVoteIndex = _votes.FindIndex(v => v.CaseId == caseId && v.UserId == actorUserId);
            if (existingVoteIndex >= 0)
            {
                return (false, "You have already voted on this case.", null);
            }

            _votes.Add(new CaseVote(caseId, actorUserId, request.Side, DateTime.UtcNow));
            AwardReward(actorUserId, "VOTE_PARTICIPATION", "CaseVote", caseId, "Thanks for participating in community judging.");

            var refreshed = MapCaseForViewer(foundCase, actorUserId);
            ReplaceCase(refreshed);
            return (true, null, refreshed);
        }
    }

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) CloseCase(Guid caseId, Guid actorUserId)
    {
        lock (_syncRoot)
        {
            var actorUser = _users.FirstOrDefault(u => u.Id == actorUserId);
            if (actorUser is null)
            {
                return (false, "Acting user not found.", null);
            }

            var foundCase = _cases.FirstOrDefault(c => c.Id == caseId);
            if (foundCase is null)
            {
                return (false, "Case not found.", null);
            }

            if (foundCase.Status == CaseStatus.Pending)
            {
                return (false, "Case is still pending acceptance and cannot be closed this way.", null);
            }

            var actorIsParticipant =
                foundCase.SideA.UserId == actorUserId ||
                (foundCase.SideB?.UserId == actorUserId);
            var actorIsModerator = actorUser.Role == UserRole.Moderator;

            if (!actorIsParticipant && !actorIsModerator)
            {
                return (false, "Only case participants or moderators can close a case.", null);
            }

            if (foundCase.Status == CaseStatus.Closed)
            {
                return (true, null, foundCase);
            }

            var closed = ResolveWinner(foundCase with { Status = CaseStatus.Closed });
            ReplaceCase(closed);

            if (closed.WinnerSide is not null)
            {
                var winnerUserId = closed.WinnerSide == CaseSide.A ? closed.SideA.UserId : closed.SideB!.UserId;
                AwardReward(winnerUserId, "CASE_VICTOR", "CaseClose", closed.Id, "Awarded for becoming the victor of this case.");

                var matchingVoterIds = _votes
                    .Where(v => v.CaseId == closed.Id && v.Side == closed.WinnerSide)
                    .Select(v => v.UserId)
                    .Distinct();

                foreach (var voterId in matchingVoterIds)
                {
                    AwardReward(voterId, "VOTE_WINNER_MATCH", "CaseClose", closed.Id, "Your vote matched the winning side.");
                }
            }

            return (true, null, closed);
        }
    }

    public IReadOnlyList<UserRewardView> GetUserRewards(Guid userId)
    {
        lock (_syncRoot)
        {
            return _rewards
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.AwardedAtUtc)
                .Select(r =>
                {
                    var badge = BadgeCatalogByCode[r.BadgeCode];
                    return new UserRewardView(r.BadgeCode, badge.Label, badge.IconKey, badge.Tier, r.Reason, r.AwardedAtUtc);
                })
                .ToList();
        }
    }

    // --- Friend system ---

    public (bool Success, string? Error) SendFriendRequest(Guid actorUserId, SendFriendRequestDto dto)
    {
        lock (_syncRoot)
        {
            if (_users.All(u => u.Id != actorUserId))
            {
                return (false, "Requesting user not found.");
            }

            if (_users.All(u => u.Id != dto.ToUserId))
            {
                return (false, "Target user not found.");
            }

            if (actorUserId == dto.ToUserId)
            {
                return (false, "You cannot send a friend request to yourself.");
            }

            var alreadyFriends = _friendRequests.Any(r =>
                r.Status == FriendRequestStatus.Accepted &&
                ((r.FromUserId == actorUserId && r.ToUserId == dto.ToUserId) ||
                 (r.FromUserId == dto.ToUserId && r.ToUserId == actorUserId)));

            if (alreadyFriends)
            {
                return (false, "You are already friends with this user.");
            }

            var pendingExists = _friendRequests.Any(r =>
                r.Status == FriendRequestStatus.Pending &&
                ((r.FromUserId == actorUserId && r.ToUserId == dto.ToUserId) ||
                 (r.FromUserId == dto.ToUserId && r.ToUserId == actorUserId)));

            if (pendingExists)
            {
                return (false, "A pending friend request already exists between these users.");
            }

            _friendRequests.Add(new FriendRequest(Guid.NewGuid(), actorUserId, dto.ToUserId, FriendRequestStatus.Pending, DateTime.UtcNow));
            return (true, null);
        }
    }

    public (bool Success, string? Error) RespondToFriendRequest(Guid requestId, Guid actorUserId, bool accept)
    {
        lock (_syncRoot)
        {
            var request = _friendRequests.FirstOrDefault(r => r.Id == requestId);
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

            var newStatus = accept ? FriendRequestStatus.Accepted : FriendRequestStatus.Declined;
            var updated = request with { Status = newStatus };
            var index = _friendRequests.FindIndex(r => r.Id == requestId);
            _friendRequests[index] = updated;
            return (true, null);
        }
    }

    public (bool Success, string? Error) RemoveFriend(Guid actorUserId, Guid friendUserId)
    {
        lock (_syncRoot)
        {
            if (_users.All(u => u.Id != actorUserId))
            {
                return (false, "Acting user not found.");
            }

            if (_users.All(u => u.Id != friendUserId))
            {
                return (false, "Friend user not found.");
            }

            if (actorUserId == friendUserId)
            {
                return (false, "You cannot remove yourself as a friend.");
            }

            var removed = _friendRequests.RemoveAll(r =>
                r.Status == FriendRequestStatus.Accepted &&
                ((r.FromUserId == actorUserId && r.ToUserId == friendUserId) ||
                 (r.FromUserId == friendUserId && r.ToUserId == actorUserId)));

            if (removed == 0)
            {
                return (false, "Users are not currently connected as friends.");
            }

            return (true, null);
        }
    }

    public IReadOnlyList<AppUser> GetFriends(Guid userId)
    {
        lock (_syncRoot)
        {
            var friendIds = _friendRequests
                .Where(r => r.Status == FriendRequestStatus.Accepted &&
                            (r.FromUserId == userId || r.ToUserId == userId))
                .Select(r => r.FromUserId == userId ? r.ToUserId : r.FromUserId)
                .Distinct()
                .ToHashSet();

            return _users.Where(u => friendIds.Contains(u.Id)).ToList();
        }
    }

    public IReadOnlyList<FriendRequest> GetFriendRequests(Guid userId)
    {
        lock (_syncRoot)
        {
            return _friendRequests
                .Where(r => r.ToUserId == userId && r.Status == FriendRequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();
        }
    }

    public IReadOnlyList<FriendRequest> GetOutgoingFriendRequests(Guid userId)
    {
        lock (_syncRoot)
        {
            return _friendRequests
                .Where(r => r.FromUserId == userId && r.Status == FriendRequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();
        }
    }

    public bool AreFriends(Guid userId, Guid otherUserId)
    {
        lock (_syncRoot)
        {
            return _friendRequests.Any(r =>
                r.Status == FriendRequestStatus.Accepted &&
                ((r.FromUserId == userId && r.ToUserId == otherUserId) ||
                 (r.FromUserId == otherUserId && r.ToUserId == userId)));
        }
    }

    // --- Case invitations ---

    public IReadOnlyList<ArgumentCase> GetPendingInvitations(Guid userId)
    {
        lock (_syncRoot)
        {
            return _cases
                .Where(c => c.Status == CaseStatus.Pending && c.InvitedUserId == userId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToList();
        }
    }

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) AcceptCaseInvitation(Guid caseId, Guid actorUserId, AcceptInvitationRequest request)
    {
        lock (_syncRoot)
        {
            var foundCase = _cases.FirstOrDefault(c => c.Id == caseId);
            if (foundCase is null)
            {
                return (false, "Case not found.", null);
            }

            if (foundCase.Status != CaseStatus.Pending)
            {
                return (false, "This case is not awaiting acceptance.", null);
            }

            if (foundCase.InvitedUserId != actorUserId)
            {
                return (false, "You are not the invited user for this case.", null);
            }

            if (string.IsNullOrWhiteSpace(request.Claim))
            {
                return (false, "A claim is required to accept the invitation.", null);
            }

            var sideBUser = _users.FirstOrDefault(u => u.Id == actorUserId);
            if (sideBUser is null)
            {
                return (false, "User not found.", null);
            }

            var sideB = new ArgumentPost(CaseSide.B, sideBUser.Id, sideBUser.UserName, request.Claim, DateTime.UtcNow);
            var opened = foundCase with { SideB = sideB, Status = CaseStatus.Open, InvitedUserId = null };
            ReplaceCase(opened);

            AwardReward(sideBUser.Id, "POST_PARTICIPATION", "CaseCreate", opened.Id, "Posted the Side B argument in a new case.");
            return (true, null, opened);
        }
    }

    public (bool Success, string? Error) DeclineCaseInvitation(Guid caseId, Guid actorUserId)
    {
        lock (_syncRoot)
        {
            var foundCase = _cases.FirstOrDefault(c => c.Id == caseId);
            if (foundCase is null)
            {
                return (false, "Case not found.");
            }

            if (foundCase.Status != CaseStatus.Pending)
            {
                return (false, "This case is not awaiting acceptance.");
            }

            if (foundCase.InvitedUserId != actorUserId)
            {
                return (false, "You are not the invited user for this case.");
            }

            var declined = foundCase with { Status = CaseStatus.Closed, InvitedUserId = null };
            ReplaceCase(declined);
            return (true, null);
        }
    }

    // --- Private helpers ---

    private ArgumentCase RefreshVerdict(ArgumentCase argumentCase)
    {
        var caseVotes = _votes.Where(v => v.CaseId == argumentCase.Id).ToList();
        var sideAVotes = caseVotes.Count(v => v.Side == CaseSide.A);
        var sideBVotes = caseVotes.Count(v => v.Side == CaseSide.B);
        return argumentCase with { Verdict = new CommunityVerdict(sideAVotes, sideBVotes) };
    }

    private CurrentUserVote? BuildCurrentUserVote(Guid caseId, Guid viewerUserId)
    {
        var vote = _votes.FirstOrDefault(v => v.CaseId == caseId && v.UserId == viewerUserId);
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

    private ArgumentCase MapCaseForViewer(ArgumentCase argumentCase, Guid? viewerUserId)
    {
        var refreshed = RefreshVerdict(argumentCase);
        if (!viewerUserId.HasValue)
        {
            return refreshed;
        }

        return refreshed with
        {
            CurrentUserVote = BuildCurrentUserVote(argumentCase.Id, viewerUserId.Value)
        };
    }

    private CaseEvidenceCollection BuildCaseEvidenceCollection(Guid caseId)
    {
        var sideA = _caseEvidence
            .Where(item => item.CaseId == caseId && item.Side == CaseSide.A)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var sideB = _caseEvidence
            .Where(item => item.CaseId == caseId && item.Side == CaseSide.B)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        return new CaseEvidenceCollection(sideA, sideB);
    }

    private (bool Success, string? Error, AppUser? User) ValidateEvidenceWrite(Guid caseId, Guid userId, CaseSide side)
    {
        var foundCase = _cases.FirstOrDefault(c => c.Id == caseId);
        if (foundCase is null)
        {
            return (false, "Case not found.", null);
        }

        if (foundCase.Status != CaseStatus.Open)
        {
            return (false, "Evidence can only be added while a case is open.", null);
        }

        var user = _users.FirstOrDefault(u => u.Id == userId);
        if (user is null)
        {
            return (false, "User not found.", null);
        }

        var sideOwnerUserId = side == CaseSide.A ? foundCase.SideA.UserId : foundCase.SideB?.UserId;
        if (!sideOwnerUserId.HasValue)
        {
            return (false, "The selected side is not active on this case.", null);
        }

        if (sideOwnerUserId.Value != userId)
        {
            return (false, "Only the owner of this side can add evidence for it.", null);
        }

        var currentEvidenceCountForSide = _caseEvidence.Count(item => item.CaseId == caseId && item.Side == side);
        if (currentEvidenceCountForSide >= MaxEvidenceItemsPerSide)
        {
            return (false, $"Side {side} already has the maximum of {MaxEvidenceItemsPerSide} evidence items.", null);
        }

        return (true, null, user);
    }

    private ArgumentCase ResolveWinner(ArgumentCase argumentCase)
    {
        var refreshed = RefreshVerdict(argumentCase);
        CaseSide? winner = null;

        if (refreshed.Verdict.VotesForSideA > refreshed.Verdict.VotesForSideB)
        {
            winner = CaseSide.A;
        }
        else if (refreshed.Verdict.VotesForSideB > refreshed.Verdict.VotesForSideA)
        {
            winner = CaseSide.B;
        }

        return refreshed with { WinnerSide = winner };
    }

    private void AwardCasePostingRewards(ArgumentCase argumentCase)
    {
        AwardReward(argumentCase.SideA.UserId, "POST_PARTICIPATION", "CaseCreate", argumentCase.Id, "Posted the Side A argument in a new case.");
        if (argumentCase.SideB is not null)
        {
            AwardReward(argumentCase.SideB.UserId, "POST_PARTICIPATION", "CaseCreate", argumentCase.Id, "Posted the Side B argument in a new case.");
        }
    }

    private void AwardReward(Guid userId, string badgeCode, string sourceType, Guid sourceId, string reason)
    {
        var alreadyAwarded = _rewards.Any(r =>
            r.UserId == userId &&
            r.BadgeCode == badgeCode &&
            r.SourceType == sourceType &&
            r.SourceId == sourceId);

        if (alreadyAwarded)
        {
            return;
        }

        _rewards.Add(new UserReward(userId, badgeCode, sourceType, sourceId, reason, DateTime.UtcNow));
    }

    private static ArgumentCase BuildCase(
        Guid id,
        string title,
        string category,
        string summary,
        ArgumentPost sideA,
        ArgumentPost sideB,
        DateTime createdAt)
    {
        return new ArgumentCase(
            id,
            title,
            category,
            summary,
            sideA,
            sideB,
            InvitedUserId: null,
            new CommunityVerdict(0, 0),
            CaseStatus.Open,
            WinnerSide: null,
            createdAt,
            CurrentUserVote: null);
    }

    private void ReplaceCase(ArgumentCase updated)
    {
        var index = _cases.FindIndex(c => c.Id == updated.Id);
        if (index >= 0)
        {
            _cases[index] = updated;
        }
    }

}
