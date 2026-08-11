using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CasesController : ControllerBase
{
    private const long MaxEvidenceFileSizeBytes = 10 * 1024 * 1024;
    private const int MaxEvidenceItemsPerSide = 20;
    private const int MaxEvidenceTitleLength = 160;
    private static readonly IReadOnlyDictionary<string, (string MimeType, CaseEvidenceType EvidenceType)> AllowedEvidenceTypes =
        new Dictionary<string, (string MimeType, CaseEvidenceType EvidenceType)>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ("image/jpeg", CaseEvidenceType.Image),
        [".jpeg"] = ("image/jpeg", CaseEvidenceType.Image),
        [".png"] = ("image/png", CaseEvidenceType.Image),
        [".webp"] = ("image/webp", CaseEvidenceType.Image),
        [".gif"] = ("image/gif", CaseEvidenceType.Image),
        [".pdf"] = ("application/pdf", CaseEvidenceType.Document),
        [".txt"] = ("text/plain", CaseEvidenceType.Document),
        [".doc"] = ("application/msword", CaseEvidenceType.Document),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", CaseEvidenceType.Document),
    };

    private readonly ICommunityCourtService _courtService;
    private readonly IActorResolver _actorResolver;
    private readonly ICaseEvidenceStorage _evidenceStorage;
    private readonly ILogger<CasesController> _logger;

    public CasesController(
        ICommunityCourtService courtService,
        IActorResolver actorResolver,
        ICaseEvidenceStorage evidenceStorage,
        ILogger<CasesController> logger)
    {
        _courtService = courtService;
        _actorResolver = actorResolver;
        _evidenceStorage = evidenceStorage;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<IEnumerable<ArgumentCase>> GetAllCases()
    {
        return Ok(_courtService.GetCases());
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ArgumentCase>> GetCaseById(Guid id, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        var match = _courtService.GetCase(id, actor?.Id);
        return match is null || !CanViewCase(match, actor)
            ? NotFound()
            : Ok(match);
    }

    [HttpGet("{id:guid}/vote-status")]
    public async Task<ActionResult<CaseVoteStatus>> GetVoteStatus(Guid id, CancellationToken cancellationToken)
    {
        if (_courtService.GetCase(id) is null)
        {
            return NotFound();
        }

        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        return Ok(new CaseVoteStatus(_courtService.HasUserVoted(id, actor.Id)));
    }

    [HttpGet("{id:guid}/evidence")]
    [AllowAnonymous]
    public async Task<ActionResult<CaseEvidenceCollection>> GetCaseEvidence(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        var foundCase = _courtService.GetCase(id, actor?.Id);
        if (foundCase is null || !CanViewCase(foundCase, actor))
        {
            return NotFound();
        }

        var evidence = _courtService.GetCaseEvidence(id);
        return Ok(new CaseEvidenceCollection(
            evidence.SideA.Select(ToApiEvidenceItem).ToArray(),
            evidence.SideB.Select(ToApiEvidenceItem).ToArray()));
    }

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/content")]
    public async Task<IActionResult> GetCaseEvidenceContent(
        Guid id,
        Guid evidenceId,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var foundCase = _courtService.GetCase(id, actor.Id);
        if (foundCase is null || !CanViewCase(foundCase, actor))
        {
            return NotFound();
        }

        var caseEvidence = _courtService.GetCaseEvidence(id);
        var evidence = caseEvidence.SideA
            .Concat(caseEvidence.SideB)
            .SingleOrDefault(item => item.Id == evidenceId && item.Type != CaseEvidenceType.Link);
        if (evidence is null)
        {
            return NotFound();
        }

        var storedContent = await _evidenceStorage.OpenReadAsync(evidence.ResourceUrl, cancellationToken);
        if (storedContent.Status == EvidenceContentStatus.NotFound)
        {
            return NotFound();
        }

        if (storedContent.Status == EvidenceContentStatus.PendingScan)
        {
            return StatusCode(
                StatusCodes.Status423Locked,
                "Evidence is awaiting malware scanning.");
        }

        if (storedContent.Status == EvidenceContentStatus.Malicious)
        {
            return StatusCode(
                StatusCodes.Status410Gone,
                "Evidence is unavailable because it failed security scanning.");
        }

        if (storedContent.Status == EvidenceContentStatus.ScanFailed)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Evidence security scanning did not complete successfully.");
        }

        var extension = Path.GetExtension(evidence.ResourceUrl);
        var downloadName = $"{SanitizeDownloadName(evidence.Title)}{extension}";
        return File(storedContent.Content!, storedContent.ContentType!, downloadName);
    }

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/status")]
    public async Task<ActionResult<CaseEvidenceStatusResponse>> GetCaseEvidenceStatus(
        Guid id,
        Guid evidenceId,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var foundCase = _courtService.GetCase(id, actor.Id);
        if (foundCase is null || !CanViewCase(foundCase, actor))
        {
            return NotFound();
        }

        var caseEvidence = _courtService.GetCaseEvidence(id);
        var evidence = caseEvidence.SideA
            .Concat(caseEvidence.SideB)
            .SingleOrDefault(item => item.Id == evidenceId && item.Type != CaseEvidenceType.Link);
        if (evidence is null)
        {
            return NotFound();
        }

        var status = await _evidenceStorage.GetStatusAsync(evidence.ResourceUrl, cancellationToken);
        return Ok(new CaseEvidenceStatusResponse(status));
    }

    [HttpPost]
    public async Task<ActionResult<ArgumentCase>> CreateCase([FromBody] CreateCaseRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.Summary) ||
            string.IsNullOrWhiteSpace(request.SideAClaim))
        {
            return BadRequest("All text fields are required.");
        }

        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        if (actor.Id == request.InvitedUserId)
        {
            return BadRequest("You cannot invite yourself to Side B.");
        }

        if (_courtService.GetUser(request.InvitedUserId) is null)
        {
            return BadRequest("Invited user does not exist.");
        }

        if (!_courtService.AreFriends(actor.Id, request.InvitedUserId))
        {
            return BadRequest("You can only invite users who are connected as friends.");
        }

        var created = _courtService.CreateCase(actor.Id, request);
        return CreatedAtAction(nameof(GetCaseById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}/comments")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<CaseComment>>> GetCaseComments(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        var foundCase = _courtService.GetCase(id, actor?.Id);
        if (foundCase is null || !CanViewCase(foundCase, actor))
        {
            return NotFound();
        }

        var comments = _courtService.GetCaseComments(id);
        return Ok(comments);
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<CaseComment>> AddCaseComment(Guid id, [FromBody] CreateCaseCommentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("Comment message is required.");
        }
        if (request.Message.Trim().Length > 1024)
        {
            return BadRequest("Comment message cannot exceed 1024 characters.");
        }

        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var result = _courtService.AddCaseComment(id, actor.Id, request);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Comment);
    }

    [HttpPost("{id:guid}/evidence/link")]
    public async Task<ActionResult<CaseEvidenceItem>> AddCaseEvidenceLink(Guid id, [FromBody] AddCaseEvidenceLinkRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var result = _courtService.AddCaseEvidenceLink(id, actor.Id, request);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Evidence);
    }

    [HttpPost("{id:guid}/evidence/upload")]
    [RequestSizeLimit(MaxEvidenceFileSizeBytes + (1024 * 1024))]
    public async Task<ActionResult<CaseEvidenceItem>> AddCaseEvidenceUpload(Guid id, [FromForm] AddCaseEvidenceUploadForm request, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        if (request.File is null)
        {
            return BadRequest("A file is required.");
        }

        if (request.File.Length == 0)
        {
            return BadRequest("Uploaded file cannot be empty.");
        }

        if (request.File.Length > MaxEvidenceFileSizeBytes)
        {
            return BadRequest($"Uploaded file cannot exceed {MaxEvidenceFileSizeBytes} bytes.");
        }

        if (!TryGetEvidenceType(request.File.FileName, request.File.ContentType, out var evidenceType))
        {
            return BadRequest("Unsupported file type. Allowed types are jpg, jpeg, png, webp, gif, pdf, txt, doc, and docx.");
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!await EvidenceFileValidator.IsValidAsync(request.File, extension, cancellationToken))
        {
            return BadRequest("Uploaded file contents do not match the selected file type.");
        }

        var evidenceTitle = string.IsNullOrWhiteSpace(request.Title)
            ? Path.GetFileNameWithoutExtension(request.File.FileName)
            : request.Title.Trim();
        if (evidenceTitle.Length == 0)
        {
            return BadRequest("Evidence title is required.");
        }

        if (evidenceTitle.Length > MaxEvidenceTitleLength)
        {
            return BadRequest($"Evidence title cannot exceed {MaxEvidenceTitleLength} characters.");
        }

        var validationError = ValidateEvidenceUploadRequest(id, actor.Id, request.Side);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        await using var uploadStream = request.File.OpenReadStream();
        var storageKey = await _evidenceStorage.UploadAsync(
            id,
            extension,
            request.File.ContentType,
            uploadStream,
            cancellationToken);

        var result = _courtService.AddCaseEvidenceFile(
            id,
            actor.Id,
            new AddCaseEvidenceFileRequest(
                request.Side,
                evidenceType,
                evidenceTitle,
                storageKey,
                request.File.ContentType,
                request.File.Length));

        if (!result.Success)
        {
            try
            {
                await _evidenceStorage.DeleteAsync(storageKey, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to delete orphaned evidence object {StorageKey} after metadata validation failed.",
                    storageKey);
            }

            return BadRequest(result.Error);
        }

        return Ok(ToApiEvidenceItem(result.Evidence!));
    }

    [HttpDelete("{id:guid}/evidence/{evidenceId:guid}")]
    public async Task<IActionResult> RemoveCaseEvidence(
        Guid id,
        Guid evidenceId,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var foundCase = _courtService.GetCase(id, actor.Id);
        if (foundCase is null)
        {
            return NotFound();
        }

        if (foundCase.Status != CaseStatus.Open)
        {
            return BadRequest("Evidence can only be removed while a case is open.");
        }

        var caseEvidence = _courtService.GetCaseEvidence(id);
        var evidence = caseEvidence.SideA
            .Concat(caseEvidence.SideB)
            .SingleOrDefault(item => item.Id == evidenceId);
        if (evidence is null)
        {
            return NotFound();
        }

        if (evidence.AddedByUserId != actor.Id)
        {
            return Forbid();
        }

        if (evidence.Type != CaseEvidenceType.Link)
        {
            try
            {
                await _evidenceStorage.DeleteAsync(evidence.ResourceUrl, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to delete evidence object {StorageKey}; metadata was retained for retry.",
                    evidence.ResourceUrl);
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Unable to remove this evidence right now. Try again later.");
            }
        }

        if (!_courtService.RemoveCaseEvidence(id, evidenceId))
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/vote")]
    public async Task<ActionResult<ArgumentCase>> CastVote(Guid id, [FromBody] CastVoteRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var result = _courtService.CastVote(id, actor.Id, request);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.UpdatedCase);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<ArgumentCase>> CloseCase(Guid id, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var result = _courtService.CloseCase(id, actor.Id);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.UpdatedCase);
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<ArgumentCase>> AcceptInvitation(Guid id, [FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var result = _courtService.AcceptCaseInvitation(id, actor.Id, request);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.UpdatedCase);
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<ActionResult> DeclineInvitation(Guid id, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var result = _courtService.DeclineCaseInvitation(id, actor.Id);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpGet("{id:guid}/result")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetResult(Guid id, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        var found = _courtService.GetCase(id, actor?.Id);
        if (found is null || !CanViewCase(found, actor))
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

    private static CaseEvidenceItem ToApiEvidenceItem(CaseEvidenceItem evidence)
    {
        if (evidence.Type == CaseEvidenceType.Link)
        {
            return evidence;
        }

        return evidence with
        {
            ResourceUrl = $"/api/cases/{evidence.CaseId}/evidence/{evidence.Id}/content",
        };
    }

    private static bool CanViewCase(ArgumentCase argumentCase, backend.Data.Entities.UserEntity? actor)
    {
        if (argumentCase.Status != CaseStatus.Pending)
        {
            return true;
        }

        return actor is not null &&
            (actor.Role == UserRole.Moderator ||
             argumentCase.SideA.UserId == actor.Id ||
             argumentCase.SideB?.UserId == actor.Id ||
             argumentCase.InvitedUserId == actor.Id);
    }

    private static string SanitizeDownloadName(string title)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(title
            .Where(character => !invalidCharacters.Contains(character))
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "evidence" : sanitized;
    }

    private static bool TryGetEvidenceType(string fileName, string contentType, out CaseEvidenceType evidenceType)
    {
        var extension = Path.GetExtension(fileName);
        if (AllowedEvidenceTypes.TryGetValue(extension, out var allowedType) &&
            string.Equals(allowedType.MimeType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            evidenceType = allowedType.EvidenceType;
            return true;
        }

        evidenceType = default;
        return false;
    }

    private string? ValidateEvidenceUploadRequest(Guid caseId, Guid userId, CaseSide side)
    {
        var foundCase = _courtService.GetCase(caseId);
        if (foundCase is null)
        {
            return "Case not found.";
        }

        if (foundCase.Status != CaseStatus.Open)
        {
            return "Evidence can only be added while a case is open.";
        }

        if (_courtService.GetUser(userId) is null)
        {
            return "User not found.";
        }

        var sideOwnerUserId = side == CaseSide.A ? foundCase.SideA.UserId : foundCase.SideB?.UserId;
        if (!sideOwnerUserId.HasValue)
        {
            return "The selected side is not active on this case.";
        }

        if (sideOwnerUserId.Value != userId)
        {
            return "Only the owner of this side can add evidence for it.";
        }

        var caseEvidence = _courtService.GetCaseEvidence(caseId);
        var evidenceCountForSide = side == CaseSide.A ? caseEvidence.SideA.Count : caseEvidence.SideB.Count;
        if (evidenceCountForSide >= MaxEvidenceItemsPerSide)
        {
            return $"Side {side} already has the maximum of {MaxEvidenceItemsPerSide} evidence items.";
        }

        return null;
    }

    public sealed class AddCaseEvidenceUploadForm
    {
        public CaseSide Side { get; set; }
        public string? Title { get; set; }
        public IFormFile? File { get; set; }
    }
}
