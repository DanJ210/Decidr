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
}
