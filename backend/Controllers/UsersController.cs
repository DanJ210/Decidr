using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ICommunityCourtService _courtService;

    public UsersController(ICommunityCourtService courtService)
    {
        _courtService = courtService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<AppUser>> GetUsers()
    {
        return Ok(_courtService.GetUsers());
    }

    [HttpGet("{id:guid}/rewards")]
    public ActionResult<IEnumerable<UserRewardView>> GetRewards(Guid id)
    {
        if (_courtService.GetUser(id) is null)
        {
            return NotFound();
        }

        return Ok(_courtService.GetUserRewards(id));
    }

    [HttpGet("{id:guid}/friends")]
    public ActionResult<IEnumerable<AppUser>> GetFriends(Guid id)
    {
        if (_courtService.GetUser(id) is null)
        {
            return NotFound();
        }

        return Ok(_courtService.GetFriends(id));
    }

    [HttpGet("{id:guid}/friend-requests")]
    public ActionResult<IEnumerable<FriendRequest>> GetFriendRequests(Guid id)
    {
        if (_courtService.GetUser(id) is null)
        {
            return NotFound();
        }

        return Ok(_courtService.GetFriendRequests(id));
    }

    [HttpGet("{id:guid}/sent-requests")]
    public ActionResult<IEnumerable<FriendRequest>> GetOutgoingFriendRequests(Guid id)
    {
        if (_courtService.GetUser(id) is null)
        {
            return NotFound();
        }

        return Ok(_courtService.GetOutgoingFriendRequests(id));
    }

    [HttpGet("{id:guid}/invitations")]
    public ActionResult<IEnumerable<ArgumentCase>> GetInvitations(Guid id)
    {
        if (_courtService.GetUser(id) is null)
        {
            return NotFound();
        }

        return Ok(_courtService.GetPendingInvitations(id));
    }
}
