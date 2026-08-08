using backend.Models;
using backend.Services;
using Xunit;

namespace backend.Tests;

public sealed class PlayerRecordCalculatorTests
{
    private readonly AppUser _alex = User("alex", "Alex");
    private readonly AppUser _blair = User("blair", "Blair");
    private readonly AppUser _casey = User("casey", "Casey");

    [Fact]
    public void Calculates_wins_losses_and_ties_from_completed_joined_cases_only()
    {
        var cases = new[]
        {
            Case(_alex, _blair, CaseStatus.Closed, CaseSide.A),
            Case(_alex, _blair, CaseStatus.Closed, CaseSide.B),
            Case(_alex, _blair, CaseStatus.Closed, null),
            Case(_alex, _blair, CaseStatus.Open, null),
            Case(_alex, null, CaseStatus.Closed, null),
        };

        var records = PlayerRecordCalculator.Calculate([_alex, _blair, _casey], cases);

        var alex = Assert.Single(records, record => record.UserId == _alex.Id);
        Assert.Equal(1, alex.Wins);
        Assert.Equal(1, alex.Losses);
        Assert.Equal(1, alex.Ties);
        Assert.Equal(3, alex.CompletedCases);
        Assert.Equal(1d / 3d, alex.WinRate);
        Assert.True(alex.IsQualified);

        var casey = Assert.Single(records, record => record.UserId == _casey.Id);
        Assert.Equal(0, casey.CompletedCases);
        Assert.False(casey.IsQualified);
        Assert.Null(casey.Rank);
    }

    [Fact]
    public void Ranks_by_win_rate_then_wins_then_completed_cases_then_name()
    {
        var drew = User("drew", "Drew");
        var cases = new[]
        {
            Case(_alex, _casey, CaseStatus.Closed, CaseSide.A),
            Case(_alex, _casey, CaseStatus.Closed, CaseSide.A),
            Case(_alex, _casey, CaseStatus.Closed, CaseSide.B),
            Case(_blair, _casey, CaseStatus.Closed, CaseSide.A),
            Case(_blair, _casey, CaseStatus.Closed, CaseSide.A),
            Case(_blair, drew, CaseStatus.Closed, CaseSide.B),
            Case(_blair, drew, CaseStatus.Closed, CaseSide.A),
            Case(drew, _casey, CaseStatus.Closed, CaseSide.A),
            Case(drew, _casey, CaseStatus.Closed, CaseSide.A),
        };

        var records = PlayerRecordCalculator.Calculate([_alex, _blair, _casey, drew], cases);

        Assert.Equal(_blair.Id, records.Single(record => record.Rank == 1).UserId);
        Assert.Equal(drew.Id, records.Single(record => record.Rank == 2).UserId);
        Assert.Equal(_alex.Id, records.Single(record => record.Rank == 3).UserId);
        Assert.Equal(_casey.Id, records.Single(record => record.Rank == 4).UserId);
    }

    [Fact]
    public void Recalculation_is_idempotent()
    {
        var cases = new[]
        {
            Case(_alex, _blair, CaseStatus.Closed, CaseSide.A),
            Case(_alex, _blair, CaseStatus.Closed, CaseSide.A),
            Case(_alex, _blair, CaseStatus.Closed, CaseSide.B),
        };

        var first = PlayerRecordCalculator.Calculate([_alex, _blair], cases);
        var second = PlayerRecordCalculator.Calculate([_alex, _blair], cases);

        Assert.Equal(first, second);
    }

    private static AppUser User(string userName, string displayName) =>
        new(Guid.NewGuid(), userName, displayName, UserRole.Member);

    private static ArgumentCase Case(
        AppUser sideA,
        AppUser? sideB,
        CaseStatus status,
        CaseSide? winnerSide)
    {
        var now = DateTime.UtcNow;
        return new ArgumentCase(
            Guid.NewGuid(),
            "Case",
            "Test",
            "Test case",
            new ArgumentPost(CaseSide.A, sideA.Id, sideA.UserName, "A", now),
            sideB is null ? null : new ArgumentPost(CaseSide.B, sideB.Id, sideB.UserName, "B", now),
            sideB is null ? Guid.NewGuid() : null,
            new CommunityVerdict(0, 0),
            status,
            winnerSide,
            now,
            null);
    }
}
