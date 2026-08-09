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

public sealed class CasesControllerEvidenceTests
{
    [Fact]
    public void Evidence_list_replaces_storage_key_with_api_content_url()
    {
        var fixture = CreateFixture();
        var evidence = CreateEvidence(fixture.CaseId, "private/blob-key.pdf");
        fixture.CourtService
            .Setup(service => service.GetCaseEvidence(fixture.CaseId))
            .Returns(new CaseEvidenceCollection([evidence], []));

        var result = fixture.Controller.GetCaseEvidence(fixture.CaseId);

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
        courtService.Setup(service => service.GetCase(caseId, null)).Returns(CreateCase(caseId, userId));
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