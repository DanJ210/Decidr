using System.Security.Claims;
using backend.Data;
using backend.Data.Entities;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class EfAuthenticatedUserService : IAuthenticatedUserService
{
    private readonly DecidirDbContext _db;

    public EfAuthenticatedUserService(DecidirDbContext db)
    {
        _db = db;
    }

    public async Task<UserEntity?> GetOrCreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var tenantId = principal.FindFirstValue("tid");
        var objectId = principal.FindFirstValue("oid");
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(objectId))
        {
            return null;
        }

        var user = await _db.Users.FirstOrDefaultAsync(
            candidate => candidate.IdentityIssuer == tenantId && candidate.IdentitySubject == objectId,
            cancellationToken);
        if (user is not null)
        {
            return user;
        }

        var email = principal.FindFirstValue("email") ?? principal.FindFirstValue("preferred_username") ?? "";
        var displayName = principal.FindFirstValue("name") ?? email;
        var userName = principal.FindFirstValue("preferred_username") ?? email;
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = $"user-{objectId}";
        }

        user = new UserEntity
        {
            Id = Guid.NewGuid(),
            IdentityIssuer = tenantId,
            IdentitySubject = objectId,
            Email = email,
            UserName = await MakeUniqueUserNameAsync(userName, cancellationToken),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "New member" : displayName,
            Role = UserRole.Member,
        };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            _db.Users.Add(user);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return user;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                _db.Entry(user).State = EntityState.Detached;
                var concurrentlyCreatedUser = await _db.Users.FirstOrDefaultAsync(
                    candidate => candidate.IdentityIssuer == tenantId && candidate.IdentitySubject == objectId,
                    cancellationToken);
                if (concurrentlyCreatedUser is not null)
                {
                    return concurrentlyCreatedUser;
                }

                user.UserName = await MakeUniqueUserNameAsync(userName, cancellationToken);
            }
        }

        throw new InvalidOperationException("The authenticated user profile could not be provisioned.");
    }

    private async Task<string> MakeUniqueUserNameAsync(string requestedName, CancellationToken cancellationToken)
    {
        var baseName = requestedName.Trim();
        if (baseName.Length > 64)
        {
            baseName = baseName[..64];
        }

        var candidate = baseName;
        var suffix = 1;
        while (await _db.Users.AnyAsync(user => user.UserName == candidate, cancellationToken))
        {
            var suffixText = $"-{suffix++}";
            candidate = baseName[..Math.Min(baseName.Length, 64 - suffixText.Length)] + suffixText;
        }

        return candidate;
    }
}