using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FriendsController : ControllerBase
{
    private readonly ICommunityCourtService _courtService;

    public FriendsController(ICommunityCourtService courtService)
    {
        _courtService = courtService;
    }

    [HttpPost("request")]
    public ActionResult SendFriendRequest([FromBody] SendFriendRequestDto dto)
    {
        var result = _courtService.SendFriendRequest(dto);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("{requestId:guid}/accept")]
    public ActionResult AcceptFriendRequest(Guid requestId, [FromBody] RespondFriendRequestDto dto)
    {
        var result = _courtService.RespondToFriendRequest(requestId, dto.ActorUserId, accept: true);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("{requestId:guid}/decline")]
    public ActionResult DeclineFriendRequest(Guid requestId, [FromBody] RespondFriendRequestDto dto)
    {
        var result = _courtService.RespondToFriendRequest(requestId, dto.ActorUserId, accept: false);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("remove")]
    public ActionResult RemoveFriend([FromBody] RemoveFriendDto dto)
    {
        var result = _courtService.RemoveFriend(dto.ActorUserId, dto.FriendUserId);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }
}
