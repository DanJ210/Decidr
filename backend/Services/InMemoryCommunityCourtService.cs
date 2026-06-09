using backend.Models;

namespace backend.Services;

public class InMemoryCommunityCourtService : ICommunityCourtService
{
    private static readonly List<RewardBadge> BadgeCatalog =
    [
        new("VOTE_PARTICIPATION", "Community Juror", "jury", "Bronze", "Awarded for participating in community voting."),
        new("VOTE_WINNER_MATCH", "Sharp Eye", "target", "Silver", "Awarded when your vote matches the winning side."),
        new("POST_PARTICIPATION", "Case Contributor", "quill", "Bronze", "Awarded for posting a side in a case."),
        new("CASE_VICTOR", "Court Victor", "crown", "Gold", "Awarded to the winning side poster when a case is closed.")
    ];

    private readonly object _syncRoot = new();
    private readonly List<AppUser> _users;
    private readonly List<ArgumentCase> _cases;
    private readonly List<CaseVote> _votes;
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

    public ArgumentCase? GetCase(Guid caseId)
    {
        lock (_syncRoot)
        {
            var found = _cases.FirstOrDefault(c => c.Id == caseId);
            return found is null ? null : RefreshVerdict(found);
        }
    }

    public bool HasUserVoted(Guid caseId, Guid userId)
    {
        lock (_syncRoot)
        {
            return _votes.Any(v => v.CaseId == caseId && v.UserId == userId);
        }
    }

    public ArgumentCase CreateCase(CreateCaseRequest request)
    {
        lock (_syncRoot)
        {
            var sideAUser = _users.First(u => u.Id == request.SideAUserId);
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
                createdAt);

            _cases.Add(created);
            AwardReward(sideAUser.Id, "POST_PARTICIPATION", "CaseCreate", created.Id, "Posted the Side A argument in a new case.");
            return created;
        }
    }

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) CastVote(Guid caseId, CastVoteRequest request)
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

            if (_users.All(u => u.Id != request.UserId))
            {
                return (false, "User not found.", null);
            }

            if (foundCase.SideA.UserId == request.UserId || foundCase.SideB?.UserId == request.UserId)
            {
                return (false, "Case participants cannot vote on their own case.", null);
            }

            var existingVoteIndex = _votes.FindIndex(v => v.CaseId == caseId && v.UserId == request.UserId);
            if (existingVoteIndex >= 0)
            {
                return (false, "You have already voted on this case.", null);
            }

            _votes.Add(new CaseVote(caseId, request.UserId, request.Side, DateTime.UtcNow));
            AwardReward(request.UserId, "VOTE_PARTICIPATION", "CaseVote", caseId, "Thanks for participating in community judging.");

            var refreshed = RefreshVerdict(foundCase);
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
            var badgeByCode = BadgeCatalog.ToDictionary(b => b.Code, b => b);

            return _rewards
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.AwardedAtUtc)
                .Select(r =>
                {
                    var badge = badgeByCode[r.BadgeCode];
                    return new UserRewardView(r.BadgeCode, badge.Label, badge.IconKey, badge.Tier, r.Reason, r.AwardedAtUtc);
                })
                .ToList();
        }
    }

    // --- Friend system ---

    public (bool Success, string? Error) SendFriendRequest(SendFriendRequestDto dto)
    {
        lock (_syncRoot)
        {
            if (_users.All(u => u.Id != dto.FromUserId))
            {
                return (false, "Requesting user not found.");
            }

            if (_users.All(u => u.Id != dto.ToUserId))
            {
                return (false, "Target user not found.");
            }

            if (dto.FromUserId == dto.ToUserId)
            {
                return (false, "You cannot send a friend request to yourself.");
            }

            var alreadyFriends = _friendRequests.Any(r =>
                r.Status == FriendRequestStatus.Accepted &&
                ((r.FromUserId == dto.FromUserId && r.ToUserId == dto.ToUserId) ||
                 (r.FromUserId == dto.ToUserId && r.ToUserId == dto.FromUserId)));

            if (alreadyFriends)
            {
                return (false, "You are already friends with this user.");
            }

            var pendingExists = _friendRequests.Any(r =>
                r.Status == FriendRequestStatus.Pending &&
                ((r.FromUserId == dto.FromUserId && r.ToUserId == dto.ToUserId) ||
                 (r.FromUserId == dto.ToUserId && r.ToUserId == dto.FromUserId)));

            if (pendingExists)
            {
                return (false, "A pending friend request already exists between these users.");
            }

            _friendRequests.Add(new FriendRequest(Guid.NewGuid(), dto.FromUserId, dto.ToUserId, FriendRequestStatus.Pending, DateTime.UtcNow));
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

    public (bool Success, string? Error, ArgumentCase? UpdatedCase) AcceptCaseInvitation(Guid caseId, AcceptInvitationRequest request)
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

            if (foundCase.InvitedUserId != request.UserId)
            {
                return (false, "You are not the invited user for this case.", null);
            }

            if (string.IsNullOrWhiteSpace(request.Claim))
            {
                return (false, "A claim is required to accept the invitation.", null);
            }

            var sideBUser = _users.FirstOrDefault(u => u.Id == request.UserId);
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
            createdAt);
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
