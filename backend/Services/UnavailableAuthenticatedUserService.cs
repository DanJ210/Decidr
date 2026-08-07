using System.Security.Claims;
using backend.Data.Entities;

namespace backend.Services;

public sealed class UnavailableAuthenticatedUserService : IAuthenticatedUserService
{
    public Task<UserEntity?> GetOrCreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<UserEntity?>(null);
    }
}