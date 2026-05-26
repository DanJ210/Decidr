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
            string.IsNullOrWhiteSpace(request.SideAClaim) ||
            string.IsNullOrWhiteSpace(request.SideBClaim))
        {
            return BadRequest("All text fields are required.");
        }

        if (request.SideAUserId == request.SideBUserId)
        {
            return BadRequest("Side A and Side B must be different users.");
        }

        if (_courtService.GetUser(request.SideAUserId) is null || _courtService.GetUser(request.SideBUserId) is null)
        {
            return BadRequest("One or more posting users do not exist.");
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
