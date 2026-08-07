using System.Security.Claims;
using backend.Data.Entities;

namespace backend.Services;

public interface IAuthenticatedUserService
{
    Task<UserEntity?> GetOrCreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}