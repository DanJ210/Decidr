using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
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
    private const long MaxCaseMediaSizeBytes = 64 * 1024 * 1024;
    private const int MaxCaseMediaDurationSeconds = 30;
    private const int MaxEvidenceItemsPerSide = 20;
    private const int MaxEvidenceTitleLength = 160;
    private static readonly IReadOnlyDictionary<string, string> AllowedCaseMediaTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".webm"] = "video/webm",
    };
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

    private static readonly ConcurrentDictionary<Guid, ConcurrentBag<PlaybackEvent>> PlaybackEvents = new();

    private readonly ICommunityCourtService _courtService;
    private readonly IActorResolver _actorResolver;
    private readonly ICaseEvidenceStorage _evidenceStorage;
    private readonly ICaseMediaUploadSessionStore _mediaUploadSessions;
    private readonly MediaUploadProcessingQueue _mediaUploadQueue;
    private readonly ILogger<CasesController> _logger;

    public CasesController(
        ICommunityCourtService courtService,
        IActorResolver actorResolver,
        ICaseEvidenceStorage evidenceStorage,
        ICaseMediaUploadSessionStore mediaUploadSessions,
        MediaUploadProcessingQueue mediaUploadQueue,
        ILogger<CasesController> logger)
    {
        _courtService = courtService;
        _actorResolver = actorResolver;
        _evidenceStorage = evidenceStorage;
        _mediaUploadSessions = mediaUploadSessions;
        _mediaUploadQueue = mediaUploadQueue;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<IEnumerable<ArgumentCase>> GetAllCases()
    {
        return Ok(_courtService.GetCases());
    }

    [HttpGet("feed")]
    [AllowAnonymous]
    public async Task<ActionResult<CaseFeedPage>> GetFeed([FromQuery] string? cursor = null, [FromQuery] int limit = 5, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 20);
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        FeedCursor? cursorValue = null;

        if (!string.IsNullOrWhiteSpace(cursor) && !TryDecodeCursor(cursor, out cursorValue))
            return BadRequest("The feed cursor is invalid.");

        var hiddenCaseIds = TrustSafetyRegistry.GetHiddenCaseIds();
        var blockedUserIds = actor is null ? [] : TrustSafetyRegistry.GetBlockedUserIds(actor.Id);
        var cases = _courtService.GetFeedCases(
            cursorValue?.CreatedAtUtc,
            cursorValue?.Id,
            limit + 1,
            hiddenCaseIds,
            blockedUserIds);
        var hasMore = cases.Count > limit;
        var page = hasMore ? cases.Take(limit).ToList() : cases.ToList();
        var nextCursor = hasMore && page.Count > 0 ? EncodeCursor(page[^1]) : null;
        return Ok(new CaseFeedPage(page, nextCursor, hasMore));
    }

    [HttpPost("{id:guid}/report")]
    public async Task<IActionResult> ReportCase(Guid id, [FromBody] ReportCaseRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        if (_courtService.GetCase(id) is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 256)
            return BadRequest("A report reason between 1 and 256 characters is required.");

        TrustSafetyRegistry.AddReport(id, actor.Id, request.Reason.Trim(), DateTime.UtcNow);
        _logger.LogInformation("Case report queued for moderation. CaseId={CaseId} ReporterId={ReporterId}", id, actor.Id);
        return Accepted();
    }

    [HttpGet("moderation/reports")]
    public async Task<ActionResult<IReadOnlyList<ModerationReport>>> GetModerationReports(CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized();
        if (actor.Role != UserRole.Moderator) return Forbid();
        return Ok(TrustSafetyRegistry.GetReports());
    }

    [HttpPost("{id:guid}/moderation")]
    public async Task<IActionResult> ModerateCase(Guid id, [FromBody] ModerateCaseRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized();
        if (actor.Role != UserRole.Moderator) return Forbid();
        if (_courtService.GetCase(id) is null) return NotFound();
        TrustSafetyRegistry.SetCaseHidden(id, request.Hidden);
        _logger.LogInformation("Case moderation state changed. CaseId={CaseId} Hidden={Hidden} ModeratorId={ModeratorId}", id, request.Hidden, actor.Id);
        return NoContent();
    }

    [HttpGet("observability/metrics")]
    public async Task<ActionResult<IReadOnlyDictionary<string, long>>> GetMetrics(CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized();
        if (actor.Role != UserRole.Moderator) return Forbid();
        return Ok(TrustSafetyRegistry.GetMetrics());
    }

    [HttpPost("{id:guid}/playback-events")]
    public async Task<IActionResult> RecordPlaybackEvent(Guid id, [FromBody] PlaybackEventRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        var foundCase = _courtService.GetCase(id);
        if (foundCase is null || !Enum.IsDefined(request.Side) || string.IsNullOrWhiteSpace(request.Event) || request.PositionSeconds < 0)
            return BadRequest("The playback event is invalid.");
        if (request.Event.Length > 32) return BadRequest("The playback event name is too long.");

        PlaybackEvents.GetOrAdd(id, _ => new ConcurrentBag<PlaybackEvent>())
            .Add(new PlaybackEvent(actor.Id, request.Side, request.Event.Trim(), request.PositionSeconds, DateTime.UtcNow));
        TrustSafetyRegistry.IncrementMetric("playback.event.accepted");
        return NoContent();
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

        var sideAMediaError = await ValidateOwnedCaseMediaAsync(actor.Id, request.SideARecordUrl, cancellationToken);
        if (sideAMediaError is not null)
        {
            return BadRequest(sideAMediaError);
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

    [HttpPost("media")]
    [RequestSizeLimit(MaxCaseMediaSizeBytes + (1024 * 1024))]
    public async Task<ActionResult<CaseMediaUploadResponse>> UploadCaseMedia(
        [FromForm] UploadCaseMediaForm request,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("A video file is required.");
        }

        if (request.File.Length > MaxCaseMediaSizeBytes)
        {
            return BadRequest($"Video cannot exceed {MaxCaseMediaSizeBytes} bytes.");
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!AllowedCaseMediaTypes.TryGetValue(extension, out var contentType))
        {
            return BadRequest("Unsupported video type. Allowed types are mp4, m4v, mov, and webm.");
        }

        if (!await VideoFileValidator.IsValidAsync(request.File, extension, cancellationToken))
        {
            return BadRequest("Uploaded file contents do not match the selected video type.");
        }

        var durationSeconds = await VideoFileValidator.GetDurationSecondsAsync(
            request.File,
            extension,
            cancellationToken);
        if (durationSeconds is null || durationSeconds > MaxCaseMediaDurationSeconds)
        {
            return BadRequest($"Video duration could not be determined or exceeds {MaxCaseMediaDurationSeconds} seconds.");
        }

        if (!TrustSafetyRegistry.TryConsumeMediaQuota(actor.Id, DateTime.UtcNow))
            return StatusCode(StatusCodes.Status429TooManyRequests, "Daily video upload quota exceeded.");

        // Media is uploaded before the case exists, so it is partitioned by uploader rather than case.
        await using var content = request.File.OpenReadStream();
        var mediaStorageKey = $"{actor.Id:N}/{Guid.NewGuid():N}{extension}";
        var storageKey = await TrustSafetyRegistry.WithRetryAsync(
            async () =>
            {
                if (content.CanSeek) content.Position = 0;
                return await _evidenceStorage.UploadAsync(
                    actor.Id,
                    extension,
                    contentType,
                    content,
                    cancellationToken,
                    mediaStorageKey);
            },
            "media.upload", cancellationToken);

        var fileName = Path.GetFileName(storageKey);
        return Ok(new CaseMediaUploadResponse(
            $"/api/cases/media/{actor.Id:N}/{fileName}",
            durationSeconds.Value,
            request.File.Length,
            contentType));
    }

    [HttpPost("media/initiate")]
    public async Task<ActionResult<CaseMediaUploadSession>> InitiateCaseMediaUpload(
        [FromBody] InitiateCaseMediaUploadRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        await PurgeExpiredMediaUploadSessionsAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest("A file name is required.");
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedCaseMediaTypes.TryGetValue(extension, out var contentType))
        {
            return BadRequest("Unsupported video type. Allowed types are mp4, m4v, mov, and webm.");
        }

        if (!string.IsNullOrWhiteSpace(request.ContentType) &&
            !string.Equals(NormalizeMediaContentType(request.ContentType), contentType, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("The requested content type does not match the file extension.");
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > MaxCaseMediaSizeBytes)
        {
            return BadRequest($"Video cannot exceed {MaxCaseMediaSizeBytes} bytes.");
        }

        if (request.DurationSeconds is <= 0 || request.DurationSeconds > MaxCaseMediaDurationSeconds)
        {
            return BadRequest($"Video duration must be between 1 and {MaxCaseMediaDurationSeconds} seconds.");
        }

        if (!TrustSafetyRegistry.TryConsumeMediaQuota(actor.Id, DateTime.UtcNow))
            return StatusCode(StatusCodes.Status429TooManyRequests, "Daily video upload quota exceeded.");

        var uploadId = Guid.NewGuid();
        var session = new CaseMediaUploadSession(
            uploadId,
            actor.Id,
            request.FileName,
            contentType,
            request.SizeBytes,
            request.DurationSeconds,
            CaseMediaUploadStatus.Pending,
            DateTime.UtcNow);

        await _mediaUploadSessions.SaveAsync(session, cancellationToken);
        return Ok(session);
    }

    [HttpPut("media/{uploadId:guid}/content")]
    [RequestSizeLimit(MaxCaseMediaSizeBytes)]
    public async Task<IActionResult> UploadCaseMediaContent(Guid uploadId, CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null) return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        var session = await _mediaUploadSessions.GetAsync(uploadId, cancellationToken);
        if (session is null) return NotFound();
        if (session.OwnerId != actor.Id) return Forbid();
        if (session.Status != CaseMediaUploadStatus.Pending) return BadRequest("This upload is no longer accepting content.");
        if (Request.ContentLength is null || Request.ContentLength != session.SizeBytes)
            return BadRequest("The uploaded content length does not match the upload session.");
        if (!string.IsNullOrWhiteSpace(Request.ContentType)
            && !string.Equals(NormalizeMediaContentType(Request.ContentType), session.ContentType, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The uploaded content type does not match the upload session.");

        var extension = Path.GetExtension(session.FileName).ToLowerInvariant();
        var storageKey = session.StorageKey ?? $"{actor.Id:N}/{uploadId:N}{extension}";
        storageKey = await _evidenceStorage.UploadAsync(
            actor.Id,
            extension,
            session.ContentType,
            Request.Body,
            cancellationToken,
            storageKey);
        TrustSafetyRegistry.IncrementMetric("media.upload.success");
        await _mediaUploadSessions.SaveAsync(session with { StorageKey = storageKey }, cancellationToken);
        return Accepted();
    }

    [HttpPost("media/{uploadId:guid}/finalize")]
    [RequestSizeLimit(MaxCaseMediaSizeBytes + (1024 * 1024))]
    public async Task<ActionResult<CaseMediaUploadResponse>> FinalizeCaseMediaUpload(
        Guid uploadId,
        [FromForm] FinalizeCaseMediaUploadRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        var session = await _mediaUploadSessions.GetAsync(uploadId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        if (session.OwnerId != actor.Id)
        {
            return Forbid();
        }

        if (session.Status != CaseMediaUploadStatus.Pending)
        {
            return BadRequest("This upload has already been finalized.");
        }

        if (request.File is null && string.IsNullOrWhiteSpace(session.StorageKey))
        {
            return BadRequest("Upload content before finalizing the media.");
        }

        if (request.File is null)
        {
            await _mediaUploadQueue.EnqueueAsync(uploadId, cancellationToken);
            await _mediaUploadSessions.SaveAsync(session with
            {
                Status = CaseMediaUploadStatus.Processing,
                FinalizedAtUtc = DateTime.UtcNow,
            }, cancellationToken);
            return Accepted(new CaseMediaUploadStatusResponse(CaseMediaUploadStatus.Processing));
        }

        if (request.File.Length == 0)
        {
            return await FailMediaUploadAsync(session, "A video file is required.", cancellationToken);
        }

        if (request.File.Length > MaxCaseMediaSizeBytes)
        {
            return await FailMediaUploadAsync(session, $"Video cannot exceed {MaxCaseMediaSizeBytes} bytes.", cancellationToken);
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!AllowedCaseMediaTypes.TryGetValue(extension, out var contentType))
        {
            return await FailMediaUploadAsync(session, "Unsupported video type. Allowed types are mp4, m4v, mov, and webm.", cancellationToken);
        }

        var sessionExtension = Path.GetExtension(session.FileName).ToLowerInvariant();
        if (!string.Equals(extension, sessionExtension, StringComparison.OrdinalIgnoreCase))
        {
            return await FailMediaUploadAsync(session, "The uploaded file extension does not match the upload session.", cancellationToken);
        }

        if (!string.Equals(contentType, session.ContentType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(NormalizeMediaContentType(request.File.ContentType), session.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return await FailMediaUploadAsync(session, "The uploaded content type does not match the upload session.", cancellationToken);
        }

        if (request.File.Length != session.SizeBytes)
        {
            return await FailMediaUploadAsync(session, "The uploaded content length does not match the upload session.", cancellationToken);
        }

        if (!await VideoFileValidator.IsValidAsync(request.File, extension, cancellationToken))
        {
            return await FailMediaUploadAsync(session, "Uploaded file contents do not match the selected video type.", cancellationToken);
        }

        var durationSeconds = await VideoFileValidator.GetDurationSecondsAsync(
            request.File,
            extension,
            cancellationToken);
        if (durationSeconds is null || durationSeconds > MaxCaseMediaDurationSeconds)
        {
            return await FailMediaUploadAsync(
                session,
                $"Video duration could not be determined or exceeds {MaxCaseMediaDurationSeconds} seconds.",
                cancellationToken);
        }

        if (session.DurationSeconds is int expectedDuration && durationSeconds.Value != expectedDuration)
        {
            return await FailMediaUploadAsync(session, "The uploaded duration does not match the upload session.", cancellationToken);
        }

        try
        {
            await using var content = request.File.OpenReadStream();
            var persistedStorageKey = session.StorageKey ?? $"{actor.Id:N}/{uploadId:N}{extension}";
            persistedStorageKey = await TrustSafetyRegistry.WithRetryAsync(
                async () =>
                {
                    if (content.CanSeek) content.Position = 0;
                    return await _evidenceStorage.UploadAsync(
                        actor.Id,
                        extension,
                        contentType,
                        content,
                        cancellationToken,
                        persistedStorageKey);
                },
                "media.upload",
                cancellationToken);

            await _mediaUploadSessions.SaveAsync(session with
            {
                Status = CaseMediaUploadStatus.Ready,
                FinalizedAtUtc = DateTime.UtcNow,
                DurationSeconds = durationSeconds,
                StorageKey = persistedStorageKey,
                Error = null,
            }, cancellationToken);

            var fileName = Path.GetFileName(persistedStorageKey);
            return Ok(new CaseMediaUploadResponse(
                $"/api/cases/media/{actor.Id:N}/{fileName}",
                durationSeconds.Value,
                request.File.Length,
                contentType));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to finalize uploaded media {UploadId}.", uploadId);
            return await FailMediaUploadAsync(session, "Video upload failed.", cancellationToken);
        }
    }

    [HttpGet("media/{uploadId:guid}/status")]
    public async Task<ActionResult<CaseMediaUploadStatusResponse>> GetCaseMediaUploadStatus(
        Guid uploadId,
        CancellationToken cancellationToken)
    {
        var actor = await _actorResolver.ResolveAsync(User, Request, cancellationToken);
        if (actor is null)
        {
            return Unauthorized("The authenticated identity could not be mapped to a Decidr profile.");
        }

        await PurgeExpiredMediaUploadSessionsAsync(cancellationToken);

        var session = await _mediaUploadSessions.GetAsync(uploadId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        if (session.OwnerId != actor.Id)
        {
            return Forbid();
        }

        var fileName = session.StorageKey is null ? null : Path.GetFileName(session.StorageKey);
        var response = session.Status == CaseMediaUploadStatus.Ready && session.StorageKey is not null
            ? new CaseMediaUploadResponse(
                $"/api/cases/media/{session.OwnerId:N}/{fileName}",
                session.DurationSeconds ?? 0,
                session.SizeBytes,
                session.ContentType)
            : null;
        return Ok(new CaseMediaUploadStatusResponse(session.Status, response, session.Error));
    }

    private async Task PurgeExpiredMediaUploadSessionsAsync(
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(24);
        var sessions = await _mediaUploadSessions.ListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            if (session.CreatedAtUtc > cutoff)
                continue;

            if (!string.IsNullOrWhiteSpace(session.StorageKey))
            {
                try
                {
                    await _evidenceStorage.DeleteAsync(session.StorageKey, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Unable to delete expired media object {StorageKey} for upload session {UploadId}.",
                        session.StorageKey,
                        session.UploadId);
                    continue;
                }
            }

            if (!await _mediaUploadSessions.DeleteAsync(session.UploadId, cancellationToken))
            {
                continue;
            }

            TrustSafetyRegistry.IncrementMetric("media.upload_sessions.purged");
        }
    }

    internal static async Task ProcessMediaUploadAsync(
        Guid uploadId,
        ICaseEvidenceStorage storage,
        ICaseMediaUploadSessionStore sessionStore,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetAsync(uploadId, cancellationToken);
        if (session is null || session.StorageKey is null)
            return;

        try
        {
            var stored = await storage.OpenReadAsync(session.StorageKey, cancellationToken);
            if (stored.Status != EvidenceContentStatus.Clean || stored.Content is null)
                throw new InvalidDataException("The uploaded media is not available for processing.");

            await using var content = stored.Content;
            var extension = Path.GetExtension(session.FileName).ToLowerInvariant();
            if (!await VideoFileValidator.IsValidAsync(content, extension, cancellationToken))
                throw new InvalidDataException("Uploaded file contents do not match the selected video type.");

            var duration = await VideoFileValidator.GetDurationSecondsAsync(content, extension, cancellationToken);
            if (duration is null || duration > MaxCaseMediaDurationSeconds)
                throw new InvalidDataException($"Video duration could not be determined or exceeds {MaxCaseMediaDurationSeconds} seconds.");

            await sessionStore.SaveAsync(session with
            {
                Status = CaseMediaUploadStatus.Ready,
                DurationSeconds = duration,
                Error = null,
            }, cancellationToken);
            TrustSafetyRegistry.IncrementMetric("media.processing.ready");
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            var failedSession = session with
            {
                Status = CaseMediaUploadStatus.Failed,
                Error = exception.Message,
            };

            try
            {
                await storage.DeleteAsync(session.StorageKey, cancellationToken);
                failedSession = failedSession with { StorageKey = null };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception cleanupException)
            {
                logger.LogWarning(
                    cleanupException,
                    "Failed to delete rejected media object {StorageKey} for upload {UploadId}.",
                    session.StorageKey,
                    uploadId);
            }

            await sessionStore.SaveAsync(failedSession, cancellationToken);
            TrustSafetyRegistry.IncrementMetric("media.processing.failed");
            logger.LogWarning(exception, "Rejected uploaded media {UploadId}.", uploadId);
        }
    }

    [HttpGet("media/{ownerId}/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCaseMedia(
        string ownerId,
        string fileName,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        if (!IsHexIdentifier(ownerId)
            || !IsHexIdentifier(Path.GetFileNameWithoutExtension(fileName))
            || !AllowedCaseMediaTypes.TryGetValue(extension, out var contentType))
        {
            return NotFound();
        }

        var storedContent = await _evidenceStorage.OpenReadAsync($"{ownerId}/{fileName}", cancellationToken);
        if (storedContent.Status != EvidenceContentStatus.Clean || storedContent.Content is null)
        {
            return NotFound();
        }

        return File(storedContent.Content, contentType, enableRangeProcessing: true);
    }

    private static bool IsHexIdentifier(string value) =>
        value.Length == 32 && value.All(Uri.IsHexDigit);

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

        var sideBMediaError = await ValidateOwnedCaseMediaAsync(actor.Id, request.SideBRecordUrl, cancellationToken);
        if (sideBMediaError is not null)
        {
            return BadRequest(sideBMediaError);
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

        var pendingCase = _courtService.GetCase(id, actor.Id);
        var result = _courtService.DeclineCaseInvitation(id, actor.Id);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        if (pendingCase is { Status: CaseStatus.Pending, InvitedUserId: Guid invitedUserId } && invitedUserId == actor.Id)
        {
            await CleanupCaseMediaAsync(pendingCase, cancellationToken);
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

    private async Task CleanupCaseMediaAsync(ArgumentCase argumentCase, CancellationToken cancellationToken)
    {
        foreach (var mediaUrl in new[] { argumentCase.SideA.MediaUrl, argumentCase.SideB?.MediaUrl })
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                continue;
            }

            var storageKey = NormalizeMediaStorageKey(mediaUrl);
            if (string.IsNullOrWhiteSpace(storageKey))
            {
                continue;
            }

            try
            {
                await _evidenceStorage.DeleteAsync(storageKey, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to clean up media object {StorageKey} after a case was declined or abandoned.",
                    storageKey);
            }
        }
    }

    private static string? NormalizeMediaStorageKey(string mediaUrl)
    {
        var trimmed = mediaUrl.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        const string mediaRoutePrefix = "/api/cases/media/";
        if (trimmed.StartsWith(mediaRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[mediaRoutePrefix.Length..];
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return null;
        }

        return trimmed;
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

    private static string EncodeCursor(ArgumentCase item)
    {
        var payload = JsonSerializer.Serialize(new FeedCursor(item.CreatedAtUtc, item.Id));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static bool TryDecodeCursor(string cursor, out FeedCursor? value)
    {
        value = null;
        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            value = JsonSerializer.Deserialize<FeedCursor>(payload);
            return value is not null;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<string?> ValidateOwnedCaseMediaAsync(Guid ownerId, string? mediaUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            return null;
        }

        if (!TryGetOwnedMediaStorageKey(ownerId, mediaUrl, out var storageKey))
        {
            return "Video URL is invalid.";
        }

        var status = await _evidenceStorage.GetStatusAsync(storageKey, cancellationToken);
        return status == EvidenceContentStatus.Clean
            ? null
            : "Video content is not available.";
    }

    private async Task<ActionResult<CaseMediaUploadResponse>> FailMediaUploadAsync(
        CaseMediaUploadSession session,
        string error,
        CancellationToken cancellationToken)
    {
        await _mediaUploadSessions.SaveAsync(session with
        {
            Status = CaseMediaUploadStatus.Failed,
            Error = error,
            FinalizedAtUtc = DateTime.UtcNow,
        }, cancellationToken);
        return BadRequest(error);
    }

    private static string? NormalizeMediaContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType)
            ? null
            : contentType.Split(';', 2)[0].Trim();

    private static bool TryGetOwnedMediaStorageKey(Guid ownerId, string mediaUrl, out string storageKey)
    {
        storageKey = string.Empty;
        var normalized = NormalizeMediaStorageKey(mediaUrl);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var separatorIndex = normalized.IndexOf('/');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var ownerSegment = normalized[..separatorIndex];
        var fileName = normalized[(separatorIndex + 1)..];
        if (!string.Equals(ownerSegment, ownerId.ToString("N"), StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(fileName) != fileName
            || !IsHexIdentifier(Path.GetFileNameWithoutExtension(fileName))
            || !AllowedCaseMediaTypes.ContainsKey(Path.GetExtension(fileName)))
        {
            return false;
        }

        storageKey = $"{ownerId:N}/{fileName}";
        return true;
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

    public sealed class UploadCaseMediaForm
    {
        public IFormFile? File { get; set; }
    }

    public enum CaseMediaUploadStatus
    {
        Pending,
        Processing,
        Ready,
        Failed,
    }

    public sealed record InitiateCaseMediaUploadRequest(
        string FileName,
        string? ContentType,
        long SizeBytes,
        int? DurationSeconds)
    {
        public InitiateCaseMediaUploadRequest() : this(string.Empty, null, 0, null)
        {
        }

        public string FileName { get; set; } = FileName;
        public string? ContentType { get; set; } = ContentType;
        public long SizeBytes { get; set; } = SizeBytes;
        public int? DurationSeconds { get; set; } = DurationSeconds;
    }

    public sealed record FinalizeCaseMediaUploadRequest
    {
        public IFormFile? File { get; set; }
    }

    public sealed record CaseMediaUploadSession(
        Guid UploadId,
        Guid OwnerId,
        string FileName,
        string ContentType,
        long SizeBytes,
        int? DurationSeconds,
        CaseMediaUploadStatus Status,
        DateTime CreatedAtUtc)
    {
        public DateTime? FinalizedAtUtc { get; init; }
        public string? StorageKey { get; init; }
        public string? Error { get; init; }
    }

    public sealed record CaseMediaUploadStatusResponse(
        CaseMediaUploadStatus Status,
        CaseMediaUploadResponse? Media = null,
        string? Error = null);

    private sealed record FeedCursor(DateTime CreatedAtUtc, Guid Id);
    private sealed record CaseReport(Guid UserId, string Reason, DateTime CreatedAtUtc);
    private sealed record PlaybackEvent(Guid UserId, CaseSide Side, string Event, int PositionSeconds, DateTime CreatedAtUtc);
}
