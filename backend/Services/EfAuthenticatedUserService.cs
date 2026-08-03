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
        var subject = principal.FindFirstValue("sub");
        var issuer = principal.FindFirstValue("iss");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer))
        {
            return null;
        }

        var user = await _db.Users.FirstOrDefaultAsync(
            candidate => candidate.IdentityIssuer == issuer && candidate.IdentitySubject == subject,
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
            userName = $"user-{subject}";
        }

        user = new UserEntity
        {
            Id = Guid.NewGuid(),
            IdentityIssuer = issuer,
            IdentitySubject = subject,
            Email = email,
            UserName = await MakeUniqueUserNameAsync(userName, cancellationToken),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "New member" : displayName,
            Role = UserRole.Member,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return user;
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