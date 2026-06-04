using backend.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;
using Microsoft.Extensions.Logging;

namespace backend.Services;

public class PersistentCommunityCourtService : ICommunityCourtService
{
    private sealed record PersistedCourtState(
        List<AppUser> Users,
        List<ArgumentCase> Cases,
        List<CaseVote> Votes,
        List<UserReward> Rewards,
        List<FriendRequest> FriendRequests);

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
    private readonly string? _connectionString;
    private readonly ILogger<PersistentCommunityCourtService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new();

    public PersistentCommunityCourtService(IConfiguration configuration, ILogger<PersistentCommunityCourtService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());

        var persistedState = TryLoadStateFromDatabase();
        if (persistedState is not null)
        {
            _users = persistedState.Users;
            _cases = persistedState.Cases;
            _votes = persistedState.Votes;
            _rewards = persistedState.Rewards;
            _friendRequests = persistedState.FriendRequests;
            return;
        }

        _users =
        [
            new(Guid.Parse("89f651a2-d6ad-43b6-a2d8-209da7599387"), "alex_t", "Alex", UserRole.Member),
            new(Guid.Parse("03a431ca-7354-43b8-b8f3-cf95f65f83b4"), "jordan_r", "Jordan", UserRole.Member),
            new(Guid.Parse("c421252a-2976-4f97-9fbf-e9f848f066f8"), "casey_l", "Casey", UserRole.Member),
            new(Guid.Parse("8af01b3a-d4b4-4954-9805-6dc58a2f0e0c"), "morgan_p", "Morgan", UserRole.Member),
            new(Guid.Parse("e1d2e6fb-c79f-4d18-8dd9-c9507487e2c4"), "sam_k", "Sam", UserRole.Moderator)
        ];

        _cases = [];

        _votes = [];

        _rewards = [];

        _friendRequests = [];
        PersistStateToDatabase();
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
            PersistStateToDatabase();
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
                var existingVote = _votes[existingVoteIndex];
                if (existingVote.Side == request.Side)
                {
                    return (false, "You already voted for this side.", null);
                }

                if (existingVote.ChangeCount >= 1)
                {
                    return (false, "You can only change your vote once.", null);
                }

                _votes[existingVoteIndex] = existingVote with
                {
                    Side = request.Side,
                    ChangeCount = existingVote.ChangeCount + 1
                };

                var updatedVerdict = RefreshVerdict(foundCase);
                ReplaceCase(updatedVerdict);
                PersistStateToDatabase();
                return (true, null, updatedVerdict);
            }

            _votes.Add(new CaseVote(caseId, request.UserId, request.Side, DateTime.UtcNow, 0));
            AwardReward(request.UserId, "VOTE_PARTICIPATION", "CaseVote", caseId, "Thanks for participating in community judging.");

            var refreshed = RefreshVerdict(foundCase);
            ReplaceCase(refreshed);
            PersistStateToDatabase();
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

            PersistStateToDatabase();
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
            PersistStateToDatabase();
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
            PersistStateToDatabase();
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

            PersistStateToDatabase();
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
            PersistStateToDatabase();
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
            PersistStateToDatabase();
            return (true, null);
        }
    }

    private PersistedCourtState? TryLoadStateFromDatabase()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            EnsureStateTable(connection);

            using var command = new NpgsqlCommand("SELECT state FROM community_court_state WHERE id = 1", connection);
            var rawState = command.ExecuteScalar() as string;
            if (string.IsNullOrWhiteSpace(rawState))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<PersistedCourtState>(rawState, _jsonOptions);
            if (state is null)
            {
                _logger.LogWarning("Community court state row exists but could not be deserialized. Falling back to seed data.");
                return null;
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load community court state from Postgres. Falling back to seed data.");
            return null;
        }
    }

    private void PersistStateToDatabase()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            EnsureStateTable(connection);

            var state = new PersistedCourtState(
                _users.ToList(),
                _cases.ToList(),
                _votes.ToList(),
                _rewards.ToList(),
                _friendRequests.ToList());

            var jsonState = JsonSerializer.Serialize(state, _jsonOptions);

            using var command = new NpgsqlCommand(@"
INSERT INTO community_court_state (id, state, updated_at)
VALUES (1, @state, NOW())
ON CONFLICT (id)
DO UPDATE SET state = EXCLUDED.state, updated_at = NOW();", connection);
            command.Parameters.Add("state", NpgsqlDbType.Jsonb).Value = jsonState;
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist community court state to Postgres.");
        }
    }

    private static void EnsureStateTable(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(@"
CREATE TABLE IF NOT EXISTS community_court_state (
    id INTEGER PRIMARY KEY,
    state JSONB NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT community_court_state_single_row CHECK (id = 1)
);", connection);

        command.ExecuteNonQuery();
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

    private void ReplaceCase(ArgumentCase updated)
    {
        var index = _cases.FindIndex(c => c.Id == updated.Id);
        if (index >= 0)
        {
            _cases[index] = updated;
        }
    }

}
