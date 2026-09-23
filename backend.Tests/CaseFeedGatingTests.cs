using backend.Data;
using backend.Data.Entities;
using backend.Models;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests;

public sealed class CaseFeedGatingTests
{
    [Theory]
    [InlineData(MediaStatus.None, false)]
    [InlineData(MediaStatus.Ready, false)]
    [InlineData(MediaStatus.Pending, true)]
    [InlineData(MediaStatus.Failed, true)]
    public void Only_unready_media_blocks_publication(MediaStatus status, bool blocks)
    {
        Assert.Equal(blocks, CaseMediaGate.BlocksPublication(status));
    }

    [Theory]
    [InlineData(null, MediaStatus.None)]
    [InlineData("", MediaStatus.None)]
    [InlineData("   ", MediaStatus.None)]
    [InlineData("/api/cases/media/abc/clip.webm", MediaStatus.Ready)]
    public void Status_is_ready_only_when_media_is_declared(string? mediaUrl, MediaStatus expected)
    {
        Assert.Equal(expected, CaseMediaGate.ResolveStatus(mediaUrl));
    }

    [Fact]
    public void Feed_requires_both_sides_to_be_ready_before_publication()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DecidirDbContext>()
            .UseSqlite(connection)
            .Options;
        using var db = new DecidirDbContext(options);
        db.Database.EnsureCreated();

        var alex = User("alex", "Alex");
        var blair = User("blair", "Blair");
        db.Users.AddRange(alex, blair);

        var textOnly = OpenCase(alex, blair, MediaStatus.None, MediaStatus.None);
        var bothReady = OpenCase(alex, blair, MediaStatus.Ready, MediaStatus.Ready);
        var defenceStillProcessing = OpenCase(alex, blair, MediaStatus.Ready, MediaStatus.Pending);
        var prosecutionFailed = OpenCase(alex, blair, MediaStatus.Failed, MediaStatus.Ready);
        var sideAOnly = OpenCase(alex, blair, MediaStatus.Ready, MediaStatus.None);
        db.Cases.AddRange(textOnly, bothReady, defenceStillProcessing, prosecutionFailed, sideAOnly);
        db.SaveChanges();

        var feed = new EfCoreCourtService(db).GetCases().Select(item => item.Id).ToList();

        Assert.DoesNotContain(textOnly.Id, feed);
        Assert.DoesNotContain(sideAOnly.Id, feed);
        Assert.Contains(bothReady.Id, feed);
        Assert.DoesNotContain(defenceStillProcessing.Id, feed);
        Assert.DoesNotContain(prosecutionFailed.Id, feed);
    }

    [Fact]
    public void In_memory_acceptance_requires_ready_media_before_public_feed_visibility()
    {
        var service = new InMemoryCommunityCourtService();
        var creator = service.GetUsers().First();
        var invitee = service.GetUsers().Skip(1).First();

        var created = service.CreateCase(creator.Id, new CreateCaseRequest(
            "Video case",
            "Culture",
            "Context",
            "Opening claim",
            invitee.Id)
        {
            SideARecordUrl = "/api/cases/media/abc/side-a.webm",
            SideADurationSeconds = 20,
        });

        Assert.Equal(MediaStatus.Ready, created.SideA.MediaStatus);
        Assert.DoesNotContain(service.GetCases(), item => item.Id == created.Id);

        var accepted = service.AcceptCaseInvitation(created.Id, invitee.Id, new AcceptInvitationRequest("Defense claim")
        {
            SideBRecordUrl = "/api/cases/media/abc/side-b.webm",
            SideBDurationSeconds = 18,
        });

        Assert.True(accepted.Success);
        Assert.Equal(MediaStatus.Ready, accepted.UpdatedCase!.SideB!.MediaStatus);
        Assert.Contains(service.GetCases(), item => item.Id == created.Id);
    }

    private static UserEntity User(string userName, string displayName) =>
        new() { Id = Guid.NewGuid(), UserName = userName, DisplayName = displayName, Role = UserRole.Member };

    private static CaseEntity OpenCase(
        UserEntity sideA,
        UserEntity sideB,
        MediaStatus sideAStatus,
        MediaStatus sideBStatus) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Case",
            Category = "Test",
            Summary = "Test",
            SideAUserId = sideA.Id,
            SideAUserName = sideA.UserName,
            SideAClaim = "A",
            SideAMediaStatus = sideAStatus,
            SideAPostedAtUtc = DateTime.UtcNow,
            SideBUserId = sideB.Id,
            SideBUserName = sideB.UserName,
            SideBClaim = "B",
            SideBMediaStatus = sideBStatus,
            SideBPostedAtUtc = DateTime.UtcNow,
            Status = CaseStatus.Open,
            CreatedAtUtc = DateTime.UtcNow,
        };
}
