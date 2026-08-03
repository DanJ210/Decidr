using backend.Models;

namespace backend.Services;

public interface ICommunityCourtService
{
    IReadOnlyList<AppUser> GetUsers();
    AppUser? GetUser(Guid userId);
    IReadOnlyList<ArgumentCase> GetCases();
    ArgumentCase? GetCase(Guid caseId, Guid? viewerUserId = null);
    IReadOnlyList<CaseComment> GetCaseComments(Guid caseId);
    CaseEvidenceCollection GetCaseEvidence(Guid caseId);
    bool HasUserVoted(Guid caseId, Guid userId);
    ArgumentCase CreateCase(Guid actorUserId, CreateCaseRequest request);
    (bool Success, string? Error, CaseComment? Comment) AddCaseComment(Guid caseId, Guid actorUserId, CreateCaseCommentRequest request);
    (bool Success, string? Error, CaseEvidenceItem? Evidence) AddCaseEvidenceLink(Guid caseId, Guid actorUserId, AddCaseEvidenceLinkRequest request);
    (bool Success, string? Error, CaseEvidenceItem? Evidence) AddCaseEvidenceFile(Guid caseId, Guid actorUserId, AddCaseEvidenceFileRequest request);
    (bool Success, string? Error, ArgumentCase? UpdatedCase) CastVote(Guid caseId, Guid actorUserId, CastVoteRequest request);
    (bool Success, string? Error, ArgumentCase? UpdatedCase) CloseCase(Guid caseId, Guid actorUserId);
    IReadOnlyList<UserRewardView> GetUserRewards(Guid userId);

    // Friend system
    (bool Success, string? Error) SendFriendRequest(SendFriendRequestDto dto);
    (bool Success, string? Error) RespondToFriendRequest(Guid requestId, Guid actorUserId, bool accept);
    (bool Success, string? Error) RemoveFriend(Guid actorUserId, Guid friendUserId);
    IReadOnlyList<AppUser> GetFriends(Guid userId);
    IReadOnlyList<FriendRequest> GetFriendRequests(Guid userId);
    IReadOnlyList<FriendRequest> GetOutgoingFriendRequests(Guid userId);
    bool AreFriends(Guid userId, Guid otherUserId);

    // Case invitations
    IReadOnlyList<ArgumentCase> GetPendingInvitations(Guid userId);
    (bool Success, string? Error, ArgumentCase? UpdatedCase) AcceptCaseInvitation(Guid caseId, AcceptInvitationRequest request);
    (bool Success, string? Error) DeclineCaseInvitation(Guid caseId, Guid actorUserId);
}
