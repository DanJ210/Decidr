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
}
