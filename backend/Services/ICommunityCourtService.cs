using backend.Models;

namespace backend.Services;

public interface ICommunityCourtService
{
    IReadOnlyList<AppUser> GetUsers();
    AppUser? GetUser(Guid userId);
    IReadOnlyList<ArgumentCase> GetCases();
    ArgumentCase? GetCase(Guid caseId);
    ArgumentCase CreateCase(CreateCaseRequest request);
    (bool Success, string? Error, ArgumentCase? UpdatedCase) CastVote(Guid caseId, CastVoteRequest request);
    (bool Success, string? Error, ArgumentCase? UpdatedCase) CloseCase(Guid caseId, Guid actorUserId);
    IReadOnlyList<UserRewardView> GetUserRewards(Guid userId);

    // Friend system
    (bool Success, string? Error) SendFriendRequest(SendFriendRequestDto dto);
    (bool Success, string? Error) RespondToFriendRequest(Guid requestId, Guid actorUserId, bool accept);
    (bool Success, string? Error) RemoveFriend(Guid actorUserId, Guid friendUserId);
    IReadOnlyList<AppUser> GetFriends(Guid userId);
    IReadOnlyList<FriendRequest> GetFriendRequests(Guid userId);
    bool AreFriends(Guid userId, Guid otherUserId);

    // Case invitations
    IReadOnlyList<ArgumentCase> GetPendingInvitations(Guid userId);
    (bool Success, string? Error, ArgumentCase? UpdatedCase) AcceptCaseInvitation(Guid caseId, AcceptInvitationRequest request);
    (bool Success, string? Error) DeclineCaseInvitation(Guid caseId, Guid actorUserId);
}
