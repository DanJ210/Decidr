using backend.Data;
using backend.Data.Entities;
using backend.Models;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests;

public sealed class PlayerRecordServiceTests
{
    [Theory]
    [InlineData(CaseSide.A, "alex_t", "jordan_r")]
    [InlineData(CaseSide.B, "jordan_r", "alex_t")]
    public void In_memory_records_side_wins_and_rejects_repeat_close(
        CaseSide winningSide,
        string winnerName,
        string loserName)
    {
        var service = new InMemoryCommunityCourtService();
        var match = service.GetCases().First();
        var voter = service.GetUsers().First(user =>
            user.Id != match.SideA.UserId && user.Id != match.SideB!.UserId);

        Assert.True(service.CastVote(match.Id, voter.Id, new CastVoteRequest(winningSide)).Success);
        Assert.True(service.CloseCase(match.Id, match.SideA.UserId).Success);
        var recordsAfterFirstClose = service.GetPlayerRecords();
        Assert.True(service.CloseCase(match.Id, match.SideA.UserId).Success);

        var records = service.GetPlayerRecords();
        Assert.Equal(recordsAfterFirstClose, records);
        Assert.Equal(1, records.Single(record => record.UserName == winnerName).Wins);
        Assert.Equal(1, records.Single(record => record.UserName == loserName).Losses);
    }

    [Fact]
    public void In_memory_records_a_tie_without_recording_losses()
    {
        var service = new InMemoryCommunityCourtService();
        var match = service.GetCases().First();

        Assert.True(service.CloseCase(match.Id, match.SideA.UserId).Success);

        var records = service.GetPlayerRecords();
        Assert.Equal(1, records.Single(record => record.UserId == match.SideA.UserId).Ties);
        Assert.Equal(0, records.Single(record => record.UserId == match.SideA.UserId).Losses);
        Assert.Equal(1, records.Single(record => record.UserId == match.SideB!.UserId).Ties);
    }

    [Fact]
    public void Ef_service_uses_the_same_authoritative_outcome_calculation()
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
        db.Cases.AddRange(
            ClosedCase(alex, blair, CaseSide.A),
            ClosedCase(alex, blair, null),
            DeclinedCase(alex, blair.Id));
        db.SaveChanges();

        var records = new EfCoreCourtService(db).GetPlayerRecords();

        var alexRecord = records.Single(record => record.UserId == alex.Id);
        var blairRecord = records.Single(record => record.UserId == blair.Id);
        Assert.Equal((1, 0, 1, 2), (alexRecord.Wins, alexRecord.Losses, alexRecord.Ties, alexRecord.CompletedCases));
        Assert.Equal((0, 1, 1, 2), (blairRecord.Wins, blairRecord.Losses, blairRecord.Ties, blairRecord.CompletedCases));
    }

    private static UserEntity User(string userName, string displayName) =>
        new() { Id = Guid.NewGuid(), UserName = userName, DisplayName = displayName, Role = UserRole.Member };

    private static CaseEntity ClosedCase(UserEntity sideA, UserEntity sideB, CaseSide? winnerSide) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Case",
            Category = "Test",
            Summary = "Test",
            SideAUserId = sideA.Id,
            SideAUserName = sideA.UserName,
            SideAClaim = "A",
            SideAPostedAtUtc = DateTime.UtcNow,
            SideBUserId = sideB.Id,
            SideBUserName = sideB.UserName,
            SideBClaim = "B",
            SideBPostedAtUtc = DateTime.UtcNow,
            Status = CaseStatus.Closed,
            WinnerSide = winnerSide,
            CreatedAtUtc = DateTime.UtcNow,
        };

    private static CaseEntity DeclinedCase(UserEntity sideA, Guid invitedUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Declined",
            Category = "Test",
            Summary = "Test",
            SideAUserId = sideA.Id,
            SideAUserName = sideA.UserName,
            SideAClaim = "A",
            SideAPostedAtUtc = DateTime.UtcNow,
            InvitedUserId = invitedUserId,
            Status = CaseStatus.Closed,
            CreatedAtUtc = DateTime.UtcNow,
        };
}
