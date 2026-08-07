using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FriendsController : ControllerBase
{
    private readonly ICommunityCourtService _courtService;
    private readonly IActorResolver _actorResolver;

    public FriendsController(ICommunityCourtService courtService, IActorResolver actorResolver)
    {
        _courtService = courtService;
        _actorResolver = actorResolver;
    }

    [HttpPost("request")]
    public async Task<ActionResult> SendFriendRequest([FromBody] SendFriendRequestDto dto, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized();

        var result = _courtService.SendFriendRequest(actor.Id, dto);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("{requestId:guid}/accept")]
    public async Task<ActionResult> AcceptFriendRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized();

        var result = _courtService.RespondToFriendRequest(requestId, actor.Id, accept: true);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("{requestId:guid}/decline")]
    public async Task<ActionResult> DeclineFriendRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized();

        var result = _courtService.RespondToFriendRequest(requestId, actor.Id, accept: false);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("remove")]
    public async Task<ActionResult> RemoveFriend([FromBody] RemoveFriendDto dto, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized();

        var result = _courtService.RemoveFriend(actor.Id, dto.FriendUserId);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }
}
