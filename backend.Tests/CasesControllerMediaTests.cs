using System.Security.Claims;
using System.Text;
using backend.Controllers;
using backend.Data.Entities;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace backend.Tests;

public sealed class CasesControllerMediaTests
{
    [Fact]
    public async Task Media_upload_stores_clip_and_returns_playback_url()
    {
        var fixture = CreateFixture();
        fixture.Storage
            .Setup(storage => storage.UploadAsync(
                fixture.UserId,
                ".webm",
                "video/webm",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync($"{fixture.UserId:N}/{Guid.NewGuid():N}.webm");

        var result = await fixture.Controller.UploadCaseMedia(
            new CasesController.UploadCaseMediaForm { File = WebmFile(12) },
            CancellationToken.None);

        var response = Assert.IsType<CaseMediaUploadResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.StartsWith($"/api/cases/media/{fixture.UserId:N}/", response.Url);
        Assert.Equal(12, response.DurationSeconds);
    }

    [Fact]
    public async Task Media_upload_reads_duration_from_iso_base_media_container()
    {
        var fixture = CreateFixture();
        fixture.Storage
            .Setup(storage => storage.UploadAsync(
                fixture.UserId,
                ".mp4",
                "video/mp4",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync($"{fixture.UserId:N}/{Guid.NewGuid():N}.mp4");

        var result = await fixture.Controller.UploadCaseMedia(
            new CasesController.UploadCaseMediaForm { File = Mp4File(18) },
            CancellationToken.None);

        var response = Assert.IsType<CaseMediaUploadResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(18, response.DurationSeconds);
    }

    [Fact]
    public async Task Media_upload_rejects_mismatched_signature_before_storage_write()
    {
        var fixture = CreateFixture();
        var content = new MemoryStream(Encoding.UTF8.GetBytes("definitely not a video"));
        var file = new FormFile(content, 0, content.Length, "file", "clip.webm")
        {
            Headers = new HeaderDictionary(),
            ContentType = "video/webm",
        };

        var result = await fixture.Controller.UploadCaseMedia(
            new CasesController.UploadCaseMediaForm { File = file },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        VerifyNoUpload(fixture);
    }

    [Theory]
    [InlineData(31)]
    public async Task Media_upload_rejects_duration_outside_the_clip_limit(int durationSeconds)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.UploadCaseMedia(
            new CasesController.UploadCaseMediaForm { File = WebmFile(durationSeconds) },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        VerifyNoUpload(fixture);
    }

    [Fact]
    public async Task Media_upload_rejects_unsupported_extension()
    {
        var fixture = CreateFixture();
        var content = new MemoryStream(Encoding.UTF8.GetBytes("whatever"));
        var file = new FormFile(content, 0, content.Length, "file", "clip.exe")
        {
            Headers = new HeaderDictionary(),
            ContentType = "video/webm",
        };

        var result = await fixture.Controller.UploadCaseMedia(
            new CasesController.UploadCaseMediaForm { File = file },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        VerifyNoUpload(fixture);
    }

    [Fact]
    public async Task Media_upload_requires_a_resolved_actor()
    {
        var fixture = CreateFixture();
        fixture.ActorResolver
            .Setup(resolver => resolver.ResolveAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<HttpRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        var result = await fixture.Controller.UploadCaseMedia(
            new CasesController.UploadCaseMediaForm { File = WebmFile(12) },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        VerifyNoUpload(fixture);
    }

    [Theory]
    [InlineData("../../secrets", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.webm")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "../../appsettings.json")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.exe")]
    public async Task Media_playback_rejects_keys_that_are_not_owned_identifiers(string ownerId, string fileName)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetCaseMedia(ownerId, fileName, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        fixture.Storage.Verify(
            storage => storage.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Media_upload_initiation_and_finalization_track_pending_then_ready_status()
    {
        var fixture = CreateFixture();
        var file = WebmFile(12);
        fixture.Storage
            .Setup(storage => storage.UploadAsync(
                fixture.UserId,
                ".webm",
                "video/webm",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ReturnsAsync($"{fixture.UserId:N}/{Guid.NewGuid():N}.webm");

        var initiate = await fixture.Controller.InitiateCaseMediaUpload(
            new CasesController.InitiateCaseMediaUploadRequest
            {
                FileName = "clip.webm",
                ContentType = "video/webm",
                SizeBytes = file.Length,
                DurationSeconds = 12,
            },
            CancellationToken.None);

        var statusBefore = Assert.IsType<OkObjectResult>(initiate.Result);
        var upload = Assert.IsType<CasesController.CaseMediaUploadSession>(statusBefore.Value);
        Assert.Equal(CasesController.CaseMediaUploadStatus.Pending, upload.Status);

        var finalise = await fixture.Controller.FinalizeCaseMediaUpload(
            upload.UploadId,
            new CasesController.FinalizeCaseMediaUploadRequest
            {
                File = file,
            },
            CancellationToken.None);

        var finalized = Assert.IsType<OkObjectResult>(finalise.Result);
        var response = Assert.IsType<CaseMediaUploadResponse>(finalized.Value);
        Assert.StartsWith($"/api/cases/media/{fixture.UserId:N}/", response.Url);
        Assert.Equal(12, response.DurationSeconds);

        var poll = await fixture.Controller.GetCaseMediaUploadStatus(
            upload.UploadId,
            CancellationToken.None);

        var pollResult = Assert.IsType<OkObjectResult>(poll.Result);
        var pollStatus = Assert.IsType<CasesController.CaseMediaUploadStatusResponse>(pollResult.Value);
        Assert.Equal(CasesController.CaseMediaUploadStatus.Ready, pollStatus.Status);
    }

    [Fact]
    public async Task Media_upload_content_then_finalize_queues_processing_and_becomes_ready()
    {
        var fixture = CreateFixture();
        var file = WebmFile(12);
        var storageKey = $"{fixture.UserId:N}/{Guid.NewGuid():N}.webm";
        fixture.Storage
            .Setup(storage => storage.UploadAsync(
                fixture.UserId,
                ".webm",
                "video/webm",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ReturnsAsync(storageKey);
        fixture.Storage
            .Setup(storage => storage.OpenReadAsync(storageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var source = file.OpenReadStream();
                return new StoredEvidenceContent(EvidenceContentStatus.Clean, source, "video/webm");
            });

        var initiate = await fixture.Controller.InitiateCaseMediaUpload(
            new CasesController.InitiateCaseMediaUploadRequest
            {
                FileName = "clip.webm",
                ContentType = "video/webm",
                SizeBytes = file.Length,
                DurationSeconds = 12,
            },
            CancellationToken.None);
        var upload = Assert.IsType<CasesController.CaseMediaUploadSession>(
            Assert.IsType<OkObjectResult>(initiate.Result).Value);

        var request = fixture.Controller.ControllerContext.HttpContext.Request;
        request.Body = file.OpenReadStream();
        request.ContentLength = file.Length;
        request.ContentType = "video/webm; charset=binary";

        var uploadContent = await fixture.Controller.UploadCaseMediaContent(upload.UploadId, CancellationToken.None);
        Assert.IsType<AcceptedResult>(uploadContent);

        var queued = await fixture.Controller.FinalizeCaseMediaUpload(
            upload.UploadId,
            new CasesController.FinalizeCaseMediaUploadRequest(),
            CancellationToken.None);
        var queuedResponse = Assert.IsType<AcceptedResult>(queued.Result);
        var queuedStatus = Assert.IsType<CasesController.CaseMediaUploadStatusResponse>(queuedResponse.Value);
        Assert.Equal(CasesController.CaseMediaUploadStatus.Processing, queuedStatus.Status);

        await CasesController.ProcessMediaUploadAsync(
            upload.UploadId,
            fixture.Storage.Object,
            fixture.SessionStore,
            Mock.Of<ILogger>(),
            CancellationToken.None);

        var poll = await fixture.Controller.GetCaseMediaUploadStatus(upload.UploadId, CancellationToken.None);
        var pollStatus = Assert.IsType<CasesController.CaseMediaUploadStatusResponse>(
            Assert.IsType<OkObjectResult>(poll.Result).Value);
        Assert.Equal(CasesController.CaseMediaUploadStatus.Ready, pollStatus.Status);
        Assert.NotNull(pollStatus.Media);
    }

    [Fact]
    public async Task Media_upload_finalization_rejects_metadata_mismatches_and_marks_session_failed()
    {
        var fixture = CreateFixture();
        var file = WebmFile(12);
        var initiate = await fixture.Controller.InitiateCaseMediaUpload(
            new CasesController.InitiateCaseMediaUploadRequest
            {
                FileName = "clip.webm",
                ContentType = "video/webm",
                SizeBytes = file.Length + 1,
                DurationSeconds = 12,
            },
            CancellationToken.None);

        var upload = Assert.IsType<CasesController.CaseMediaUploadSession>(
            Assert.IsType<OkObjectResult>(initiate.Result).Value);

        var finalise = await fixture.Controller.FinalizeCaseMediaUpload(
            upload.UploadId,
            new CasesController.FinalizeCaseMediaUploadRequest
            {
                File = file,
            },
            CancellationToken.None);

        var failure = Assert.IsType<BadRequestObjectResult>(finalise.Result);
        Assert.Equal("The uploaded content length does not match the upload session.", failure.Value);

        var poll = await fixture.Controller.GetCaseMediaUploadStatus(upload.UploadId, CancellationToken.None);
        var pollStatus = Assert.IsType<CasesController.CaseMediaUploadStatusResponse>(
            Assert.IsType<OkObjectResult>(poll.Result).Value);
        Assert.Equal(CasesController.CaseMediaUploadStatus.Failed, pollStatus.Status);
    }

    [Fact]
    public async Task Declining_a_case_deletes_any_attached_side_a_media()
    {
        var caseId = Guid.NewGuid();
        var service = new Mock<ICommunityCourtService>();
        service
            .Setup(x => x.DeclineCaseInvitation(caseId, It.IsAny<Guid>()))
            .Returns((true, null));

        var fixture = CreateFixture(service.Object);
        var actorId = fixture.UserId;
        var pendingCase = new ArgumentCase(
            caseId,
            "Title",
            "Category",
            "Summary",
            new ArgumentPost(CaseSide.A, Guid.NewGuid(), "creator", "Claim", DateTime.UtcNow)
            {
                MediaUrl = $"/api/cases/media/{Guid.NewGuid():N}/side-a.webm",
                MediaStatus = MediaStatus.Ready,
            },
            null,
            actorId,
            new CommunityVerdict(0, 0),
            CaseStatus.Pending,
            null,
            DateTime.UtcNow,
            null);
        service
            .Setup(x => x.GetCase(caseId, It.IsAny<Guid?>()))
            .Returns(pendingCase);

        var result = await fixture.Controller.DeclineInvitation(caseId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        fixture.Storage.Verify(
            storage => storage.DeleteAsync(
                It.Is<string>(key => key.EndsWith("/side-a.webm", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void VerifyNoUpload(MediaFixture fixture) =>
        fixture.Storage.Verify(
            storage => storage.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

    private static FormFile WebmFile(int durationSeconds)
    {
        var durationBytes = BitConverter.GetBytes(durationSeconds * 1_000f);
        if (BitConverter.IsLittleEndian) Array.Reverse(durationBytes);
        var bytes = new byte[]
        {
            0x1A, 0x45, 0xDF, 0xA3, 0x80,
            0x18, 0x53, 0x80, 0x67, 0xFF,
            0x15, 0x49, 0xA9, 0x66, 0x8E,
            0x2A, 0xD7, 0xB1, 0x83, 0x0F, 0x42, 0x40,
            0x44, 0x89, 0x84,
        }.Concat(durationBytes).ToArray();
        var content = new MemoryStream(bytes);
        return new FormFile(content, 0, content.Length, "file", "clip.webm")
        {
            Headers = new HeaderDictionary(),
            ContentType = "video/webm",
        };
    }

    private static FormFile Mp4File(int durationSeconds)
    {
        var duration = durationSeconds * 1_000;
        var bytes = new byte[]
        {
            0x00, 0x00, 0x00, 0x10, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x24, 0x6D, 0x6F, 0x6F, 0x76,
            0x00, 0x00, 0x00, 0x1C, 0x6D, 0x76, 0x68, 0x64,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x03, 0xE8,
            (byte)(duration >> 24), (byte)(duration >> 16), (byte)(duration >> 8), (byte)duration,
        };
        var content = new MemoryStream(bytes);
        return new FormFile(content, 0, content.Length, "file", "clip.mp4")
        {
            Headers = new HeaderDictionary(),
            ContentType = "video/mp4",
        };
    }

    private static MediaFixture CreateFixture(ICommunityCourtService? service = null)
    {
        var userId = Guid.NewGuid();
        var actorResolver = new Mock<IActorResolver>();
        actorResolver
            .Setup(resolver => resolver.ResolveAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<HttpRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEntity { Id = userId });

        var storage = new Mock<ICaseEvidenceStorage>();
        var sessionStore = new TestCaseMediaUploadSessionStore();
        var controller = new CasesController(
            service ?? Mock.Of<ICommunityCourtService>(),
            actorResolver.Object,
            storage.Object,
            sessionStore,
            new MediaUploadProcessingQueue(),
            Mock.Of<ILogger<CasesController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        return new MediaFixture(userId, actorResolver, storage, sessionStore, controller);
    }

    private sealed record MediaFixture(
        Guid UserId,
        Mock<IActorResolver> ActorResolver,
        Mock<ICaseEvidenceStorage> Storage,
        TestCaseMediaUploadSessionStore SessionStore,
        CasesController Controller);
}
