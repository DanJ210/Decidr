using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ICommunityCourtService _courtService;
    private readonly IAuthenticatedUserService _authenticatedUserService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public UsersController(
        ICommunityCourtService courtService,
        IAuthenticatedUserService authenticatedUserService,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _courtService = courtService;
        _authenticatedUserService = authenticatedUserService;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet]
    public ActionResult<IEnumerable<AppUser>> GetUsers()
    {
        return Ok(_courtService.GetUsers());
    }

    [HttpGet("{id:guid}/rewards")]
    public async Task<ActionResult<IEnumerable<UserRewardView>>> GetRewards(Guid id, CancellationToken cancellationToken)
    {
        if (!await CanAccessUserAsync(id, cancellationToken))
        {
            return Unauthorized();
        }

        return Ok(_courtService.GetUserRewards(id));
    }

    [HttpGet("{id:guid}/friends")]
    public async Task<ActionResult<IEnumerable<AppUser>>> GetFriends(Guid id, CancellationToken cancellationToken)
    {
        if (!await CanAccessUserAsync(id, cancellationToken))
        {
            return Unauthorized();
        }

        return Ok(_courtService.GetFriends(id));
    }

    [HttpGet("{id:guid}/friend-requests")]
    public async Task<ActionResult<IEnumerable<FriendRequest>>> GetFriendRequests(Guid id, CancellationToken cancellationToken)
    {
        if (!await CanAccessUserAsync(id, cancellationToken))
        {
            return Unauthorized();
        }

        return Ok(_courtService.GetFriendRequests(id));
    }

    [HttpGet("{id:guid}/sent-requests")]
    public async Task<ActionResult<IEnumerable<FriendRequest>>> GetOutgoingFriendRequests(Guid id, CancellationToken cancellationToken)
    {
        if (!await CanAccessUserAsync(id, cancellationToken))
        {
            return Unauthorized();
        }

        return Ok(_courtService.GetOutgoingFriendRequests(id));
    }

    [HttpGet("{id:guid}/invitations")]
    public async Task<ActionResult<IEnumerable<ArgumentCase>>> GetInvitations(Guid id, CancellationToken cancellationToken)
    {
        if (!await CanAccessUserAsync(id, cancellationToken))
        {
            return Unauthorized();
        }

        return Ok(_courtService.GetPendingInvitations(id));
    }

    private async Task<bool> CanAccessUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var actor = await _authenticatedUserService.GetOrCreateAsync(User, cancellationToken);
            return actor?.Id == userId;
        }

        return _environment.IsDevelopment() &&
            string.IsNullOrWhiteSpace(_configuration["Entra:Authority"]) &&
            string.IsNullOrWhiteSpace(_configuration["Entra:Audience"]);
    }
}
