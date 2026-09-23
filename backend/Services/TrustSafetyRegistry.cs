using System.Collections.Concurrent;

namespace backend.Services;

public sealed record ModerationReport(Guid CaseId, Guid ReporterId, string Reason, DateTime CreatedAtUtc);

public static class TrustSafetyRegistry
{
    private const int DailyMediaUploadQuota = 10;
    private static readonly ConcurrentDictionary<Guid, ConcurrentBag<ModerationReport>> Reports = new();
    private static readonly ConcurrentDictionary<string, byte> Blocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> HiddenCases = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> DailyUploads = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, long> Metrics = new(StringComparer.Ordinal);

    public static bool TryConsumeMediaQuota(Guid userId, DateTime nowUtc)
    {
        var key = UploadKey(userId, nowUtc);
        var count = DailyUploads.AddOrUpdate(key, 1, (_, current) => current + 1);
        if (count <= DailyMediaUploadQuota)
        {
            IncrementMetric("media.upload.accepted");
            return true;
        }

        DailyUploads.AddOrUpdate(key, 0, (_, current) => Math.Max(0, current - 1));
        IncrementMetric("media.upload.quota_rejected");
        return false;
    }

    public static void AddReport(Guid caseId, Guid reporterId, string reason, DateTime createdAtUtc)
    {
        Reports.GetOrAdd(caseId, _ => new ConcurrentBag<ModerationReport>())
            .Add(new ModerationReport(caseId, reporterId, reason, createdAtUtc));
        IncrementMetric("moderation.reported");
    }

    public static IReadOnlyList<ModerationReport> GetReports() =>
        Reports.Values.SelectMany(items => items).OrderByDescending(item => item.CreatedAtUtc).ToList();

    public static void SetCaseHidden(Guid caseId, bool hidden)
    {
        if (hidden) HiddenCases[caseId.ToString("N")] = 0;
        else HiddenCases.TryRemove(caseId.ToString("N"), out _);
        IncrementMetric(hidden ? "moderation.hidden" : "moderation.restored");
    }

    public static bool IsCaseHidden(Guid caseId) => HiddenCases.ContainsKey(caseId.ToString("N"));

    public static void Block(Guid actorId, Guid targetId) => Blocks[BlockKey(actorId, targetId)] = 0;

    public static void Unblock(Guid actorId, Guid targetId) => Blocks.TryRemove(BlockKey(actorId, targetId), out _);

    public static bool IsBlockedEither(Guid firstUserId, Guid secondUserId) =>
        Blocks.ContainsKey(BlockKey(firstUserId, secondUserId)) ||
        Blocks.ContainsKey(BlockKey(secondUserId, firstUserId));

    public static void IncrementMetric(string name) => Metrics.AddOrUpdate(name, 1, (_, count) => count + 1);

    public static IReadOnlyDictionary<string, long> GetMetrics() =>
        new Dictionary<string, long>(Metrics);

    public static int PurgeExpiredUploadSessions<T>(ConcurrentDictionary<Guid, T> sessions, Func<T, DateTime> createdAt, DateTime nowUtc, TimeSpan retention)
    {
        var removed = 0;
        foreach (var item in sessions)
        {
            if (nowUtc - createdAt(item.Value) <= retention || !sessions.TryRemove(item.Key, out _)) continue;
            removed++;
        }

        if (removed > 0) IncrementMetric("media.upload_sessions.purged");
        return removed;
    }

    public static async Task<T> WithRetryAsync<T>(Func<Task<T>> operation, string metricName, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var result = await operation();
                IncrementMetric(metricName + ".success");
                return result;
            }
            catch when (attempt < 3)
            {
                IncrementMetric(metricName + ".retry");
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
            }
        }
    }

    private static string UploadKey(Guid userId, DateTime nowUtc) => $"{userId:N}:{nowUtc:yyyy-MM-dd}";
    private static string BlockKey(Guid actorId, Guid targetId) => $"{actorId:N}:{targetId:N}";
}
