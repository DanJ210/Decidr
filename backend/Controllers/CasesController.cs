using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CasesController : ControllerBase
{
    private const long MaxEvidenceFileSizeBytes = 10 * 1024 * 1024;
    private const int MaxEvidenceItemsPerSide = 20;
    private const int MaxEvidenceTitleLength = 160;
    private static readonly IReadOnlySet<string> AllowedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };
    private static readonly IReadOnlySet<string> AllowedDocumentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".txt",
        ".doc",
        ".docx"
    };
    private static readonly IReadOnlySet<string> AllowedImageMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };
    private static readonly IReadOnlySet<string> AllowedDocumentMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "text/plain",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private readonly ICommunityCourtService _courtService;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public CasesController(ICommunityCourtService courtService, IWebHostEnvironment webHostEnvironment)
    {
        _courtService = courtService;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ArgumentCase>> GetAllCases()
    {
        return Ok(_courtService.GetCases());
    }

    [HttpGet("{id:guid}")]
    public ActionResult<ArgumentCase> GetCaseById(Guid id, [FromQuery] Guid? userId = null)
    {
        var match = _courtService.GetCase(id, userId);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpGet("{id:guid}/vote-status")]
    public ActionResult<CaseVoteStatus> GetVoteStatus(Guid id, [FromQuery] Guid userId)
    {
        if (_courtService.GetCase(id) is null)
        {
            return NotFound();
        }

        if (_courtService.GetUser(userId) is null)
        {
            return BadRequest("User not found.");
        }

        return Ok(new CaseVoteStatus(_courtService.HasUserVoted(id, userId)));
    }

    [HttpGet("{id:guid}/evidence")]
    public ActionResult<CaseEvidenceCollection> GetCaseEvidence(Guid id)
    {
        if (_courtService.GetCase(id) is null)
        {
            return NotFound();
        }

        return Ok(_courtService.GetCaseEvidence(id));
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

    [HttpGet("{id:guid}/comments")]
    public ActionResult<IEnumerable<CaseComment>> GetCaseComments(Guid id)
    {
        var comments = _courtService.GetCaseComments(id);
        if (comments.Count > 0)
        {
            return Ok(comments);
        }

        if (_courtService.GetCase(id) is null)
        {
            return NotFound();
        }

        return Ok(comments);
    }

    [HttpPost("{id:guid}/comments")]
    public ActionResult<CaseComment> AddCaseComment(Guid id, [FromBody] CreateCaseCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("Comment message is required.");
        }
        if (request.Message.Trim().Length > 1024)
        {
            return BadRequest("Comment message cannot exceed 1024 characters.");
        }

        var result = _courtService.AddCaseComment(id, request);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Comment);
    }

    [HttpPost("{id:guid}/evidence/link")]
    public ActionResult<CaseEvidenceItem> AddCaseEvidenceLink(Guid id, [FromBody] AddCaseEvidenceLinkRequest request)
    {
        var result = _courtService.AddCaseEvidenceLink(id, request);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Evidence);
    }

    [HttpPost("{id:guid}/evidence/upload")]
    [RequestSizeLimit(MaxEvidenceFileSizeBytes + (1024 * 1024))]
    public async Task<ActionResult<CaseEvidenceItem>> AddCaseEvidenceUpload(Guid id, [FromForm] AddCaseEvidenceUploadForm request)
    {
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

        var validationError = ValidateEvidenceUploadRequest(id, request.UserId, request.Side);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var caseDirectory = Path.Combine(GetEvidenceUploadRootPath(), id.ToString("N"));
        Directory.CreateDirectory(caseDirectory);

        var fullPath = Path.Combine(caseDirectory, safeFileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await request.File.CopyToAsync(stream);
        }

        var publicResourceUrl = $"/uploads/case-evidence/{id:N}/{safeFileName}";

        var result = _courtService.AddCaseEvidenceFile(
            id,
            new AddCaseEvidenceFileRequest(
                request.UserId,
                request.Side,
                evidenceType,
                evidenceTitle,
                publicResourceUrl,
                request.File.ContentType,
                request.File.Length));

        if (!result.Success)
        {
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }

            return BadRequest(result.Error);
        }

        return Ok(result.Evidence);
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

    private string GetEvidenceUploadRootPath()
    {
        var webRootPath = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
        }

        return Path.Combine(webRootPath, "uploads", "case-evidence");
    }

    private static bool TryGetEvidenceType(string fileName, string contentType, out CaseEvidenceType evidenceType)
    {
        var extension = Path.GetExtension(fileName);
        if (AllowedImageExtensions.Contains(extension) && AllowedImageMimeTypes.Contains(contentType))
        {
            evidenceType = CaseEvidenceType.Image;
            return true;
        }

        if (AllowedDocumentExtensions.Contains(extension) && AllowedDocumentMimeTypes.Contains(contentType))
        {
            evidenceType = CaseEvidenceType.Document;
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
        public Guid UserId { get; set; }
        public CaseSide Side { get; set; }
        public string? Title { get; set; }
        public IFormFile? File { get; set; }
    }
}
