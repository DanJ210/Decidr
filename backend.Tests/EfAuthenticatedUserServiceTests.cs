using System.Security.Claims;
using backend.Data;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests;

public sealed class EfAuthenticatedUserServiceTests
{
    [Fact]
    public async Task Raw_entra_claims_create_and_reuse_one_profile()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        await using var db = new DecidirDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new EfAuthenticatedUserService(db);
        var principal = CreatePrincipal(Guid.NewGuid(), Guid.NewGuid(), "alex@example.com");

        var created = await service.GetOrCreateAsync(principal);
        var reused = await service.GetOrCreateAsync(principal);

        Assert.NotNull(created);
        Assert.Same(created, reused);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(principal.FindFirstValue("tid"), created.IdentityIssuer);
        Assert.Equal(principal.FindFirstValue("oid"), created.IdentitySubject);
    }

    [Fact]
    public async Task Missing_stable_entra_identifier_is_rejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        await using var db = new DecidirDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new EfAuthenticatedUserService(db);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("iss", "https://issuer.example")],
            "Bearer"));

        var user = await service.GetOrCreateAsync(principal);

        Assert.Null(user);
        Assert.Empty(db.Users);
    }

    private static DbContextOptions<DecidirDbContext> CreateOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<DecidirDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static ClaimsPrincipal CreatePrincipal(Guid tenantId, Guid objectId, string userName)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tid", tenantId.ToString()),
                new Claim("oid", objectId.ToString()),
                new Claim("preferred_username", userName),
                new Claim("name", "Alex Example"),
                new Claim("scp", "access_as_user"),
            ],
            "Bearer"));
    }
}