using System.Security.Claims;
using backend.Controllers;
using backend.Data.Entities;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace backend.Tests;

public sealed class UsersControllerTests
{
    [Fact]
    public async Task Private_read_allows_resolved_actor_matching_route_user()
    {
        var userId = Guid.NewGuid();
        var courtService = new Mock<ICommunityCourtService>();
        courtService.Setup(service => service.GetFriends(userId)).Returns([]);
        var actorResolver = new Mock<IActorResolver>();
        actorResolver
            .Setup(resolver => resolver.ResolveAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<HttpRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEntity { Id = userId });
        var controller = CreateController(courtService, actorResolver);

        var result = await controller.GetFriends(userId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        courtService.Verify(service => service.GetFriends(userId), Times.Once);
    }

    [Fact]
    public async Task Private_read_rejects_missing_or_mismatched_development_actor()
    {
        var routeUserId = Guid.NewGuid();
        var courtService = new Mock<ICommunityCourtService>();
        var actorResolver = new Mock<IActorResolver>();
        actorResolver
            .SetupSequence(resolver => resolver.ResolveAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<HttpRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null)
            .ReturnsAsync(new UserEntity { Id = Guid.NewGuid() });
        var controller = CreateController(courtService, actorResolver);

        var missingActorResult = await controller.GetFriends(routeUserId, CancellationToken.None);
        var mismatchedActorResult = await controller.GetFriends(routeUserId, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(missingActorResult.Result);
        Assert.IsType<UnauthorizedResult>(mismatchedActorResult.Result);
        courtService.Verify(service => service.GetFriends(It.IsAny<Guid>()), Times.Never);
    }

    private static UsersController CreateController(
        Mock<ICommunityCourtService> courtService,
        Mock<IActorResolver> actorResolver)
    {
        return new UsersController(courtService.Object, actorResolver.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
    }
}