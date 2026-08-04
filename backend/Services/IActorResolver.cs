using System.Security.Claims;
using backend.Data.Entities;

namespace backend.Services;

public interface IActorResolver
{
    Task<UserEntity?> ResolveAsync(ClaimsPrincipal principal, HttpRequest request, CancellationToken cancellationToken = default);
}