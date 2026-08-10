using System.Security.Claims;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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

public sealed class CasesControllerEvidenceTests
{
    [Theory]
    [InlineData(null, EvidenceContentStatus.PendingScan)]
    [InlineData("No threats found", EvidenceContentStatus.Clean)]
    [InlineData("Malicious", EvidenceContentStatus.Malicious)]
    [InlineData("Error", EvidenceContentStatus.ScanFailed)]
    [InlineData("Not scanned", EvidenceContentStatus.ScanFailed)]
    [InlineData("Unexpected", EvidenceContentStatus.PendingScan)]
    public void Defender_scan_tag_maps_to_fail_closed_status(
        string? scanResult,
        EvidenceContentStatus expectedStatus)
    {
        var tags = new Dictionary<string, string>();
        if (scanResult is not null)
        {
            tags[AzureBlobCaseEvidenceStorage.MalwareScanResultTag] = scanResult;
        }

        var status = AzureBlobCaseEvidenceStorage.GetEvidenceContentStatus(tags);

        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public async Task Storage_status_maps_unexpected_azure_failure_to_scan_failed()
    {
        const string storageKey = "case-id/private-object.pdf";
        var blobClient = new Mock<BlobClient>();
        blobClient
            .Setup(client => client.GetTagsAsync(
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(
                StatusCodes.Status503ServiceUnavailable,
                "Storage unavailable",
                "ServerBusy",
                null));
        var containerClient = new Mock<BlobContainerClient>();
        containerClient
            .Setup(client => client.GetBlobClient(storageKey))
            .Returns(blobClient.Object);
        var storage = new AzureBlobCaseEvidenceStorage(
            containerClient.Object,
            Mock.Of<ILogger<AzureBlobCaseEvidenceStorage>>());

        var status = await storage.GetStatusAsync(storageKey, CancellationToken.None);

        Assert.Equal(EvidenceContentStatus.ScanFailed, status);
    }

    [Fact]
    public async Task Evidence_list_replaces_storage_key_with_api_content_url()
    {
        var fixture = CreateFixture();
        var evidence = CreateEvidence(fixture.CaseId, "private/blob-key.pdf");
        fixture.CourtService
            .Setup(service => service.GetCaseEvidence(fixture.CaseId))
            .Returns(new CaseEvidenceCollection([evidence], []));

        var result = await fixture.Controller.GetCaseEvidence(fixture.CaseId, CancellationToken.None);

        var response = Assert.IsType<CaseEvidenceCollection>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(
            $"/api/cases/{fixture.CaseId}/evidence/{evidence.Id}/content",
            Assert.Single(response.SideA).ResourceUrl);
        Assert.DoesNotContain("private/blob-key", response.SideA[0].ResourceUrl);
    }

    [Fact]
    public async Task Upload_stores_opaque_key_and_returns_api_content_url()
    {
        var fixture = CreateFixture();
        const string storageKey = "case-id/private-object.pdf";
        var evidenceId = Guid.NewGuid();
        AddCaseEvidenceFileRequest? savedRequest = null;
        fixture.Storage
            .Setup(storage => storage.UploadAsync(
                fixture.CaseId,
                ".pdf",
                "application/pdf",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storageKey);
        fixture.CourtService
            .Setup(service => service.AddCaseEvidenceFile(
                fixture.CaseId,
                fixture.UserId,
                It.IsAny<AddCaseEvidenceFileRequest>()))
            .Callback<Guid, Guid, AddCaseEvidenceFileRequest>((_, _, request) => savedRequest = request)
            .Returns(() =>
            {
                var request = savedRequest!;
                return (
                    true,
                    (string?)null,
                    (CaseEvidenceItem?)new CaseEvidenceItem(
                        evidenceId,
                        fixture.CaseId,
                        request.Side,
                        fixture.UserId,
                        "alex_t",
                        request.Type,
                        request.Title,
                        request.ResourceUrl,
                        request.MimeType,
                        request.SizeBytes,
                        DateTime.UtcNow));
            });
        var form = CreatePdfUpload();

        var result = await fixture.Controller.AddCaseEvidenceUpload(
            fixture.CaseId,
            form,
            CancellationToken.None);

        var response = Assert.IsType<CaseEvidenceItem>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(storageKey, savedRequest!.ResourceUrl);
        Assert.Equal($"/api/cases/{fixture.CaseId}/evidence/{evidenceId}/content", response.ResourceUrl);
    }

    [Fact]
    public async Task Upload_deletes_object_when_metadata_write_fails()
    {
        var fixture = CreateFixture();
        const string storageKey = "case-id/orphan.pdf";
        fixture.Storage
            .Setup(storage => storage.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storageKey);
        fixture.CourtService
            .Setup(service => service.AddCaseEvidenceFile(
                fixture.CaseId,
                fixture.UserId,
                It.IsAny<AddCaseEvidenceFileRequest>()))
            .Returns((false, "Metadata rejected.", null));

        var result = await fixture.Controller.AddCaseEvidenceUpload(
            fixture.CaseId,
            CreatePdfUpload(),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        fixture.Storage.Verify(
            storage => storage.DeleteAsync(storageKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Upload_rejects_mismatched_file_signature_before_storage_write()
    {
        var fixture = CreateFixture();
        var content = new MemoryStream(Encoding.UTF8.GetBytes("not actually a PDF"));
        var file = new FormFile(content, 0, content.Length, "file", "evidence.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };

        var result = await fixture.Controller.AddCaseEvidenceUpload(
            fixture.CaseId,
            new CasesController.AddCaseEvidenceUploadForm
            {
                Side = CaseSide.A,
                Title = "Evidence",
                File = file,
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        fixture.Storage.Verify(
            storage => storage.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("evidence.pdf", "text/plain")]
    [InlineData("evidence.txt", "application/pdf")]
    [InlineData("evidence.png", "image/jpeg")]
    [InlineData("evidence.jpg", "image/png")]
    public async Task Upload_rejects_mismatched_extension_and_content_type_before_storage_write(
        string fileName,
        string contentType)
    {
        var fixture = CreateFixture();
        var content = new MemoryStream("test content"u8.ToArray());
        var file = new FormFile(content, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

        var result = await fixture.Controller.AddCaseEvidenceUpload(
            fixture.CaseId,
            new CasesController.AddCaseEvidenceUploadForm
            {
                Side = CaseSide.A,
                Title = "Evidence",
                File = file,
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(
            "Unsupported file type. Allowed types are jpg, jpeg, png, webp, gif, pdf, txt, doc, and docx.",
            badRequest.Value);
        fixture.Storage.Verify(
            storage => storage.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Content_endpoint_streams_private_object()
    {
        var fixture = CreateFixture();
        var evidence = CreateEvidence(fixture.CaseId, "private/blob-key.pdf");
        fixture.CourtService
            .Setup(service => service.GetCaseEvidence(fixture.CaseId))
            .Returns(new CaseEvidenceCollection([evidence], []));
        fixture.Storage
            .Setup(storage => storage.OpenReadAsync(evidence.ResourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredEvidenceContent(
                EvidenceContentStatus.Clean,
                new MemoryStream(Encoding.UTF8.GetBytes("file contents")),
                "application/pdf"));

        var result = await fixture.Controller.GetCaseEvidenceContent(
            fixture.CaseId,
            evidence.Id,
            CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("Evidence.pdf", file.FileDownloadName);
    }

    [Theory]
    [InlineData(EvidenceContentStatus.PendingScan, StatusCodes.Status423Locked)]
    [InlineData(EvidenceContentStatus.Malicious, StatusCodes.Status410Gone)]
    [InlineData(EvidenceContentStatus.ScanFailed, StatusCodes.Status503ServiceUnavailable)]
    public async Task Content_endpoint_blocks_object_without_clean_scan_result(
        EvidenceContentStatus storageStatus,
        int expectedStatusCode)
    {
        var fixture = CreateFixture();
        var evidence = CreateEvidence(fixture.CaseId, "private/blob-key.pdf");
        fixture.CourtService
            .Setup(service => service.GetCaseEvidence(fixture.CaseId))
            .Returns(new CaseEvidenceCollection([evidence], []));
        fixture.Storage
            .Setup(storage => storage.OpenReadAsync(evidence.ResourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredEvidenceContent(storageStatus));

        var result = await fixture.Controller.GetCaseEvidenceContent(
            fixture.CaseId,
            evidence.Id,
            CancellationToken.None);

        var blocked = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, blocked.StatusCode);
    }

    [Fact]
    public async Task Content_endpoint_returns_not_found_for_missing_object()
    {
        var fixture = CreateFixture();
        var evidence = CreateEvidence(fixture.CaseId, "private/blob-key.pdf");
        fixture.CourtService
            .Setup(service => service.GetCaseEvidence(fixture.CaseId))
            .Returns(new CaseEvidenceCollection([evidence], []));
        fixture.Storage
            .Setup(storage => storage.OpenReadAsync(evidence.ResourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredEvidenceContent(EvidenceContentStatus.NotFound));

        var result = await fixture.Controller.GetCaseEvidenceContent(
            fixture.CaseId,
            evidence.Id,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(EvidenceContentStatus.PendingScan)]
    [InlineData(EvidenceContentStatus.Clean)]
    [InlineData(EvidenceContentStatus.Malicious)]
    [InlineData(EvidenceContentStatus.ScanFailed)]
    [InlineData(EvidenceContentStatus.NotFound)]
    public async Task Status_endpoint_returns_current_storage_status(EvidenceContentStatus storageStatus)
    {
        var fixture = CreateFixture();
        var evidence = CreateEvidence(fixture.CaseId, "private/blob-key.pdf");
        fixture.CourtService
            .Setup(service => service.GetCaseEvidence(fixture.CaseId))
            .Returns(new CaseEvidenceCollection([evidence], []));
        fixture.Storage
            .Setup(storage => storage.GetStatusAsync(evidence.ResourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storageStatus);

        var result = await fixture.Controller.GetCaseEvidenceStatus(
            fixture.CaseId,
            evidence.Id,
            CancellationToken.None);

        var response = Assert.IsType<CaseEvidenceStatusResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(storageStatus, response.Status);
    }

    [Fact]
    public async Task Content_endpoint_rejects_unresolved_actor()
    {
        var fixture = CreateFixture();
        fixture.ActorResolver
            .Setup(resolver => resolver.ResolveAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<HttpRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        var result = await fixture.Controller.GetCaseEvidenceContent(
            fixture.CaseId,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        fixture.Storage.Verify(
            storage => storage.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static EvidenceFixture CreateFixture()
    {
        var caseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courtService = new Mock<ICommunityCourtService>();
        courtService
            .Setup(service => service.GetCase(caseId, It.IsAny<Guid?>()))
            .Returns(CreateCase(caseId, userId));
        courtService.Setup(service => service.GetUser(userId)).Returns(new AppUser(userId, "alex_t", "Alex", UserRole.Member));
        courtService.Setup(service => service.GetCaseEvidence(caseId)).Returns(new CaseEvidenceCollection([], []));

        var actorResolver = new Mock<IActorResolver>();
        actorResolver
            .Setup(resolver => resolver.ResolveAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<HttpRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEntity { Id = userId });

        var storage = new Mock<ICaseEvidenceStorage>();
        var controller = new CasesController(
            courtService.Object,
            actorResolver.Object,
            storage.Object,
            Mock.Of<ILogger<CasesController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        return new EvidenceFixture(caseId, userId, courtService, actorResolver, storage, controller);
    }

    private static ArgumentCase CreateCase(Guid caseId, Guid userId) => new(
        caseId,
        "Title",
        "Category",
        "Summary",
        new ArgumentPost(CaseSide.A, userId, "alex_t", "Claim", DateTime.UtcNow),
        new ArgumentPost(CaseSide.B, Guid.NewGuid(), "jordan_r", "Claim", DateTime.UtcNow),
        null,
        new CommunityVerdict(0, 0),
        CaseStatus.Open,
        null,
        DateTime.UtcNow,
        null);

    private static CaseEvidenceItem CreateEvidence(Guid caseId, string storageKey) => new(
        Guid.NewGuid(),
        caseId,
        CaseSide.A,
        Guid.NewGuid(),
        "alex_t",
        CaseEvidenceType.Document,
        "Evidence",
        storageKey,
        "application/pdf",
        128,
        DateTime.UtcNow);

    private static CasesController.AddCaseEvidenceUploadForm CreatePdfUpload()
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 evidence"));
        var file = new FormFile(content, 0, content.Length, "file", "evidence.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
        return new CasesController.AddCaseEvidenceUploadForm
        {
            Side = CaseSide.A,
            Title = "Evidence",
            File = file,
        };
    }

    private sealed record EvidenceFixture(
        Guid CaseId,
        Guid UserId,
        Mock<ICommunityCourtService> CourtService,
        Mock<IActorResolver> ActorResolver,
        Mock<ICaseEvidenceStorage> Storage,
        CasesController Controller);
}