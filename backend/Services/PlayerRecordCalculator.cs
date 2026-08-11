using backend.Models;

namespace backend.Services;

public static class PlayerRecordCalculator
{
    public const int MinimumCompletedCases = 3;

    public static IReadOnlyList<PlayerRecord> Calculate(
        IEnumerable<AppUser> users,
        IEnumerable<ArgumentCase> cases)
    {
        var completedCases = cases
            .Where(c => c.Status == CaseStatus.Closed && c.SideB is not null)
            .ToList();

        var records = users.Select(user =>
        {
            var userCases = completedCases
                .Where(c => c.SideA.UserId == user.Id || c.SideB!.UserId == user.Id)
                .ToList();
            var wins = userCases.Count(c =>
                (c.WinnerSide == CaseSide.A && c.SideA.UserId == user.Id) ||
                (c.WinnerSide == CaseSide.B && c.SideB!.UserId == user.Id));
            var ties = userCases.Count(c => c.WinnerSide is null);
            var losses = userCases.Count - wins - ties;
            var winRate = userCases.Count == 0 ? 0 : (double)wins / userCases.Count;

            return new PlayerRecord(
                user.Id,
                user.UserName,
                user.DisplayName,
                wins,
                losses,
                ties,
                userCases.Count,
                winRate,
                userCases.Count >= MinimumCompletedCases,
                Rank: null);
        }).ToList();

        var ranked = records
            .Where(record => record.IsQualified)
            .OrderByDescending(record => record.WinRate)
            .ThenByDescending(record => record.Wins)
            .ThenByDescending(record => record.CompletedCases)
            .ThenBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rankByUserId = ranked
            .Select((record, index) => new { record.UserId, Rank = index + 1 })
            .ToDictionary(item => item.UserId, item => item.Rank);

        return records
            .Select(record => rankByUserId.TryGetValue(record.UserId, out var rank)
                ? record with { Rank = rank }
                : record)
            .OrderBy(record => record.Rank is null)
            .ThenBy(record => record.Rank)
            .ThenByDescending(record => record.CompletedCases)
            .ThenBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
