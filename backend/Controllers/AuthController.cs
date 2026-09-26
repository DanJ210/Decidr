using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticatedUserService _authenticatedUserService;

    public AuthController(IAuthenticatedUserService authenticatedUserService)
    {
        _authenticatedUserService = authenticatedUserService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var user = await _authenticatedUserService.GetOrCreateAsync(User, cancellationToken);
        return user is null
            ? Unauthorized("The authenticated identity could not be mapped to a Decidr profile.")
            : Ok(new
            {
                user.Id,
                user.UserName,
                user.DisplayName,
                user.Email,
                user.Role,
            });
    }
}