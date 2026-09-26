using System.Security.Claims;
using backend.Controllers;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace backend.Tests;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Anonymous_request_returns_unauthorized_without_invoking_user_service()
    {
        var authenticatedUserService = new Mock<IAuthenticatedUserService>();
        var controller = new AuthController(authenticatedUserService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

        var result = await controller.GetCurrentUser(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        authenticatedUserService.Verify(
            service => service.GetOrCreateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}