using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CasesController : ControllerBase
{
    private readonly ICommunityCourtService _courtService;

    public CasesController(ICommunityCourtService courtService)
    {
        _courtService = courtService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ArgumentCase>> GetAllCases()
    {
        return Ok(_courtService.GetCases());
    }

    [HttpGet("{id:guid}")]
    public ActionResult<ArgumentCase> GetCaseById(Guid id)
    {
        var match = _courtService.GetCase(id);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPost]
    public ActionResult<ArgumentCase> CreateCase([FromBody] CreateCaseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.Summary) ||
            string.IsNullOrWhiteSpace(request.SideAClaim))
        {
            return BadRequest("All text fields are required.");
        }

        if (request.SideAUserId == request.InvitedUserId)
        {
            return BadRequest("You cannot invite yourself to Side B.");
        }

        if (_courtService.GetUser(request.SideAUserId) is null)
        {
            return BadRequest("Side A user does not exist.");
        }

        if (_courtService.GetUser(request.InvitedUserId) is null)
        {
            return BadRequest("Invited user does not exist.");
        }

        if (!_courtService.AreFriends(request.SideAUserId, request.InvitedUserId))
        {
            return BadRequest("You can only invite users who are connected as friends.");
        }

        var created = _courtService.CreateCase(request);
        return CreatedAtAction(nameof(GetCaseById), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/vote")]
    public ActionResult<ArgumentCase> CastVote(Guid id, [FromBody] CastVoteRequest request)
    {
        var result = _courtService.CastVote(id, request);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.UpdatedCase);
    }

    [HttpPost("{id:guid}/close")]
    public ActionResult<ArgumentCase> CloseCase(Guid id, [FromBody] CloseCaseRequest request)
    {
        var result = _courtService.CloseCase(id, request.ActorUserId);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.UpdatedCase);
    }

    [HttpPost("{id:guid}/accept")]
    public ActionResult<ArgumentCase> AcceptInvitation(Guid id, [FromBody] AcceptInvitationRequest request)
    {
        var result = _courtService.AcceptCaseInvitation(id, request);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.UpdatedCase);
    }

    [HttpPost("{id:guid}/decline")]
    public ActionResult DeclineInvitation(Guid id, [FromBody] DeclineInvitationRequest request)
    {
        var result = _courtService.DeclineCaseInvitation(id, request.UserId);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpGet("{id:guid}/result")]
    public ActionResult<object> GetResult(Guid id)
    {
        var found = _courtService.GetCase(id);
        if (found is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            found.Id,
            found.Status,
            found.WinnerSide,
            found.Verdict
        });
    }
}
