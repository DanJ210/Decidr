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
            new CasesController.UploadCaseMediaForm { DurationSeconds = 12, File = WebmFile() },
            CancellationToken.None);

        var response = Assert.IsType<CaseMediaUploadResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.StartsWith($"/api/cases/media/{fixture.UserId:N}/", response.Url);
        Assert.Equal(12, response.DurationSeconds);
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
            new CasesController.UploadCaseMediaForm { DurationSeconds = 10, File = file },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        VerifyNoUpload(fixture);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task Media_upload_rejects_duration_outside_the_clip_limit(int durationSeconds)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.UploadCaseMedia(
            new CasesController.UploadCaseMediaForm { DurationSeconds = durationSeconds, File = WebmFile() },
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
            new CasesController.UploadCaseMediaForm { DurationSeconds = 10, File = file },
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
            new CasesController.UploadCaseMediaForm { DurationSeconds = 10, File = WebmFile() },
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

    private static void VerifyNoUpload(MediaFixture fixture) =>
        fixture.Storage.Verify(
            storage => storage.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

    private static FormFile WebmFile()
    {
        var bytes = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var content = new MemoryStream(bytes);
        return new FormFile(content, 0, content.Length, "file", "clip.webm")
        {
            Headers = new HeaderDictionary(),
            ContentType = "video/webm",
        };
    }

    private static MediaFixture CreateFixture()
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
        var controller = new CasesController(
            Mock.Of<ICommunityCourtService>(),
            actorResolver.Object,
            storage.Object,
            Mock.Of<ILogger<CasesController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        return new MediaFixture(userId, actorResolver, storage, controller);
    }

    private sealed record MediaFixture(
        Guid UserId,
        Mock<IActorResolver> ActorResolver,
        Mock<ICaseEvidenceStorage> Storage,
        CasesController Controller);
}
