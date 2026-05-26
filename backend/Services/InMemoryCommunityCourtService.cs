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
        SeedVotes(firstCaseId, 118, 143);
        SeedVotes(secondCaseId, 209, 96);

        _rewards = [];
        AwardCasePostingRewards(_cases[0]);
        AwardCasePostingRewards(_cases[1]);
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

    public ArgumentCase CreateCase(CreateCaseRequest request)
    {
        lock (_syncRoot)
        {
            var sideAUser = _users.First(u => u.Id == request.SideAUserId);
            var sideBUser = _users.First(u => u.Id == request.SideBUserId);
            var createdAt = DateTime.UtcNow;

            var created = BuildCase(
                Guid.NewGuid(),
                request.Title,
                request.Category,
                request.Summary,
                new ArgumentPost(CaseSide.A, sideAUser.Id, sideAUser.UserName, request.SideAClaim, createdAt),
                new ArgumentPost(CaseSide.B, sideBUser.Id, sideBUser.UserName, request.SideBClaim, createdAt),
                createdAt);

            _cases.Add(created);
            AwardCasePostingRewards(created);
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

            if (foundCase.Status == CaseStatus.Closed)
            {
                return (false, "Case is closed and can no longer receive votes.", null);
            }

            if (_users.All(u => u.Id != request.UserId))
            {
                return (false, "User not found.", null);
            }

            if (_votes.Any(v => v.CaseId == caseId && v.UserId == request.UserId))
            {
                return (false, "User has already voted on this case.", null);
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

            var actorIsParticipant =
                foundCase.SideA.UserId == actorUserId ||
                foundCase.SideB.UserId == actorUserId;
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
                var winnerUserId = closed.WinnerSide == CaseSide.A ? closed.SideA.UserId : closed.SideB.UserId;
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
        AwardReward(argumentCase.SideB.UserId, "POST_PARTICIPATION", "CaseCreate", argumentCase.Id, "Posted the Side B argument in a new case.");
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
            new CommunityVerdict(0, 0),
            CaseStatus.Open,
            null,
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

    private void SeedVotes(Guid caseId, int sideAVotes, int sideBVotes)
    {
        for (var i = 0; i < sideAVotes; i++)
        {
            _votes.Add(new CaseVote(caseId, Guid.NewGuid(), CaseSide.A, DateTime.UtcNow.AddMinutes(-i)));
        }

        for (var i = 0; i < sideBVotes; i++)
        {
            _votes.Add(new CaseVote(caseId, Guid.NewGuid(), CaseSide.B, DateTime.UtcNow.AddMinutes(-i - 2)));
        }
    }
}
