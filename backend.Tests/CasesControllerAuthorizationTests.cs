using System.Reflection;
using System.Security.Claims;
using backend.Controllers;
using backend.Data.Entities;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace backend.Tests;

public sealed class CasesControllerAuthorizationTests
{
    [Theory]
    [InlineData(nameof(CasesController.GetAllCases))]
    [InlineData(nameof(CasesController.GetCaseById))]
    [InlineData(nameof(CasesController.GetCaseComments))]
    [InlineData(nameof(CasesController.GetCaseEvidence))]
    [InlineData(nameof(CasesController.GetResult))]
    public void Intended_public_reads_are_explicitly_anonymous(string methodName)
    {
        var method = typeof(CasesController).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task Anonymous_user_can_read_open_case()
    {
        var argumentCase = CreateCase(CaseStatus.Open);
        var fixture = CreateFixture(argumentCase, actor: null);

        var result = await fixture.Controller.GetCaseById(argumentCase.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Anonymous_user_cannot_read_pending_case()
    {
        var argumentCase = CreateCase(CaseStatus.Pending);
        var fixture = CreateFixture(argumentCase, actor: null);

        var result = await fixture.Controller.GetCaseById(argumentCase.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Invited_user_can_read_pending_case()
    {
        var invitedUserId = Guid.NewGuid();
        var argumentCase = CreateCase(CaseStatus.Pending, invitedUserId);
        var fixture = CreateFixture(argumentCase, new UserEntity
        {
            Id = invitedUserId,
            Role = UserRole.Member,
        });

        var result = await fixture.Controller.GetCaseById(argumentCase.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Unrelated_user_cannot_read_pending_case_resources()
    {
        var argumentCase = CreateCase(CaseStatus.Pending, Guid.NewGuid());
        var fixture = CreateFixture(argumentCase, new UserEntity
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Member,
        });

        var comments = await fixture.Controller.GetCaseComments(argumentCase.Id, CancellationToken.None);
        var evidence = await fixture.Controller.GetCaseEvidence(argumentCase.Id, CancellationToken.None);
        var result = await fixture.Controller.GetResult(argumentCase.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(comments.Result);
        Assert.IsType<NotFoundResult>(evidence.Result);
        Assert.IsType<NotFoundResult>(result.Result);
        fixture.CourtService.Verify(service => service.GetCaseComments(It.IsAny<Guid>()), Times.Never);
        fixture.CourtService.Verify(service => service.GetCaseEvidence(It.IsAny<Guid>()), Times.Never);
    }

    private static AuthorizationFixture CreateFixture(ArgumentCase argumentCase, UserEntity? actor)
    {
        var courtService = new Mock<ICommunityCourtService>();
        courtService
            .Setup(service => service.GetCase(argumentCase.Id, It.IsAny<Guid?>()))
            .Returns(argumentCase);

        var actorResolver = new Mock<IActorResolver>();
        actorResolver
            .Setup(resolver => resolver.ResolveAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<HttpRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var controller = new CasesController(
            courtService.Object,
            actorResolver.Object,
            Mock.Of<ICaseEvidenceStorage>(),
            Mock.Of<ILogger<CasesController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        return new AuthorizationFixture(courtService, controller);
    }

    private static ArgumentCase CreateCase(CaseStatus status, Guid? invitedUserId = null) => new(
        Guid.NewGuid(),
        "Title",
        "Category",
        "Summary",
        new ArgumentPost(CaseSide.A, Guid.NewGuid(), "alex_t", "Claim", DateTime.UtcNow),
        null,
        invitedUserId,
        new CommunityVerdict(0, 0),
        status,
        null,
        DateTime.UtcNow,
        null);

    private sealed record AuthorizationFixture(
        Mock<ICommunityCourtService> CourtService,
        CasesController Controller);
}