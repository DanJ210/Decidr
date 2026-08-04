using System.Security.Claims;
using backend.Data.Entities;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace backend.Tests;

public sealed class ActorResolverTests
{
    [Fact]
    public async Task Authenticated_identity_wins_over_development_header()
    {
        var claimActor = new UserEntity
        {
            Id = Guid.NewGuid(),
            UserName = "entra-user",
            DisplayName = "Entra User",
            Role = UserRole.Member,
        };
        var headerActor = new AppUser(Guid.NewGuid(), "header-user", "Header User", UserRole.Member);
        var authService = new Mock<IAuthenticatedUserService>();
        authService
            .Setup(service => service.GetOrCreateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimActor);
        var courtService = new Mock<ICommunityCourtService>();
        courtService.Setup(service => service.GetUser(headerActor.Id)).Returns(headerActor);
        var resolver = CreateResolver(authService, courtService, isDevelopment: true);
        var request = CreateRequest(headerActor.Id);
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));

        var resolved = await resolver.ResolveAsync(principal, request);

        Assert.Same(claimActor, resolved);
        authService.Verify(service => service.GetOrCreateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
        courtService.Verify(service => service.GetUser(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Development_header_resolves_seeded_user_without_entra_configuration()
    {
        var expected = new AppUser(Guid.NewGuid(), "casey_l", "Casey", UserRole.Member);
        var authService = new Mock<IAuthenticatedUserService>();
        var courtService = new Mock<ICommunityCourtService>();
        courtService.Setup(service => service.GetUser(expected.Id)).Returns(expected);
        var resolver = CreateResolver(authService, courtService, isDevelopment: true);

        var resolved = await resolver.ResolveAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            CreateRequest(expected.Id));

        Assert.NotNull(resolved);
        Assert.Equal(expected.Id, resolved.Id);
        Assert.Equal(expected.UserName, resolved.UserName);
        Assert.Equal(expected.DisplayName, resolved.DisplayName);
        authService.Verify(service => service.GetOrCreateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Development_header_is_rejected_when_entra_is_configured()
    {
        var expected = new AppUser(Guid.NewGuid(), "casey_l", "Casey", UserRole.Member);
        var authService = new Mock<IAuthenticatedUserService>();
        var courtService = new Mock<ICommunityCourtService>();
        courtService.Setup(service => service.GetUser(expected.Id)).Returns(expected);
        var resolver = CreateResolver(authService, courtService, isDevelopment: true, authority: "https://tenant.example/", audience: "api://decidr");

        var resolved = await resolver.ResolveAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            CreateRequest(expected.Id));

        Assert.Null(resolved);
        courtService.Verify(service => service.GetUser(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Development_header_is_rejected_outside_development()
    {
        var expected = new AppUser(Guid.NewGuid(), "casey_l", "Casey", UserRole.Member);
        var authService = new Mock<IAuthenticatedUserService>();
        var courtService = new Mock<ICommunityCourtService>();
        courtService.Setup(service => service.GetUser(expected.Id)).Returns(expected);
        var resolver = CreateResolver(authService, courtService, isDevelopment: false);

        var resolved = await resolver.ResolveAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            CreateRequest(expected.Id));

        Assert.Null(resolved);
        courtService.Verify(service => service.GetUser(It.IsAny<Guid>()), Times.Never);
    }

    private static ActorResolver CreateResolver(
        Mock<IAuthenticatedUserService> authService,
        Mock<ICommunityCourtService> courtService,
        bool isDevelopment,
        string? authority = null,
        string? audience = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Entra:Authority"] = authority,
            ["Entra:Audience"] = audience,
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        return new ActorResolver(authService.Object, courtService.Object, configuration, environment.Object);
    }

    private static HttpRequest CreateRequest(Guid userId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Dev-User-Id"] = userId.ToString();
        return context.Request;
    }
}
