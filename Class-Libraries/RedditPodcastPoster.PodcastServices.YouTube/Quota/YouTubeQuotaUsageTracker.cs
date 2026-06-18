using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public sealed class YouTubeQuotaUsageTracker(IOptions<YouTubeSettings> settings) : IYouTubeQuotaUsageTracker
{
    private readonly YouTubeSettings _settings =
        settings.Value ?? throw new ArgumentNullException($"Missing {nameof(YouTubeSettings)}.");

    private readonly ConcurrentDictionary<string, MutableKeyStats> _stats = new(StringComparer.Ordinal);

    public void RecordCall(Application application, ApplicationUsage usage)
    {
        GetOrAdd(application, usage).CallsAttempted++;
    }

    public void RecordQuotaHit(Application application, ApplicationUsage usage)
    {
        GetOrAdd(application, usage).QuotaHits++;
    }

    public void RecordQuotaConsumed(Application application, ApplicationUsage usage, int quotaUnits)
    {
        if (quotaUnits <= 0)
        {
            return;
        }

        GetOrAdd(application, usage).QuotaUsed += quotaUnits;
    }

    public YouTubeQuotaDailyReport CreateReport(DateOnly reportDate, string sourceApplication)
    {
        var (usedIndexerKeys, unusedIndexerKeys) = BuildIndexerKeyUsage();

        return new YouTubeQuotaDailyReport
        {
            Id = YouTubeQuotaDailyReport.CreateId(reportDate, sourceApplication),
            ReportDate = reportDate,
            SourceApplication = sourceApplication,
            Keys = _stats.Values
                .OrderBy(x => x.Usage)
                .ThenBy(x => x.Project)
                .ThenBy(x => x.DisplayName)
                .Select(CreateKeyStats)
                .ToList(),
            UsedIndexerKeys = usedIndexerKeys,
            UnusedIndexerKeys = unusedIndexerKeys
        };
    }

    public void Reset()
    {
        _stats.Clear();
    }

    private (List<YouTubeIndexerKeySummary> Used, List<YouTubeIndexerKeySummary> Unused) BuildIndexerKeyUsage()
    {
        var used = new List<YouTubeIndexerKeySummary>();
        var unused = new List<YouTubeIndexerKeySummary>();

        foreach (var application in GetConfiguredIndexerApplications())
        {
            var summary = CreateIndexerKeySummary(application);

            if (summary.CallsAttempted > 0 || summary.QuotaHits > 0 || summary.QuotaUsed > 0)
            {
                used.Add(summary);
            }
            else
            {
                unused.Add(summary);
            }
        }

        return (used, unused);
    }

    private IEnumerable<Application> GetConfiguredIndexerApplications() =>
        _settings.Applications
            .Where(x => x.Usage == ApplicationUsage.Indexer)
            .OrderBy(ResolveHourPrimary)
            .ThenBy(x => x.Reattempt ?? 0)
            .ThenBy(x => x.DisplayName, StringComparer.Ordinal);

    private YouTubeIndexerKeySummary CreateIndexerKeySummary(Application application)
    {
        var statsKey = CreateStatsKey(application.ApiKey, ApplicationUsage.Indexer);
        _stats.TryGetValue(statsKey, out var stats);
        var callsAttempted = stats?.CallsAttempted ?? 0;
        var quotaHits = stats?.QuotaHits ?? 0;
        var quotaUsed = stats?.QuotaUsed ?? 0;

        return new YouTubeIndexerKeySummary
        {
            DisplayName = application.DisplayName,
            Project = application.Name,
            HourPrimary = ResolveHourPrimary(application),
            Reattempt = application.Reattempt,
            ApiKeySuffix = ResolveApiKeySuffix(application.ApiKey),
            CallsAttempted = callsAttempted,
            QuotaHits = quotaHits,
            QuotaUsed = quotaUsed,
            DailyLimit = YouTubeQuotaCosts.DailyLimitPerKey,
            RemainingQuota = ResolveRemainingQuota(quotaUsed, quotaHits)
        };
    }

    private static YouTubeQuotaKeyStats CreateKeyStats(MutableKeyStats stats) =>
        new()
        {
            DisplayName = stats.DisplayName,
            Project = stats.Project,
            Usage = stats.Usage,
            CallsAttempted = stats.CallsAttempted,
            QuotaHits = stats.QuotaHits,
            QuotaUsed = stats.QuotaUsed,
            DailyLimit = YouTubeQuotaCosts.DailyLimitPerKey,
            RemainingQuota = ResolveRemainingQuota(stats.QuotaUsed, stats.QuotaHits),
            CapacityHint = ResolveCapacityHint(stats.CallsAttempted, stats.QuotaHits)
        };

    private MutableKeyStats GetOrAdd(Application application, ApplicationUsage usage)
    {
        var key = CreateStatsKey(application.ApiKey, usage);
        return _stats.GetOrAdd(key, _ => new MutableKeyStats
        {
            DisplayName = application.DisplayName,
            Project = application.Name,
            Usage = usage.ToString()
        });
    }

    internal static string CreateStatsKey(string apiKey, ApplicationUsage usage) => $"{usage}:{apiKey}";

    internal static int ResolveHourPrimary(Application application)
    {
        var parts = application.DisplayName.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3
            && parts[0] == "Indexer"
            && parts[1] == "HourPrimary"
            && int.TryParse(parts[2], out var hourPrimary))
        {
            return hourPrimary;
        }

        throw new InvalidOperationException(
            $"Unable to resolve hour primary from indexer display name '{application.DisplayName}'.");
    }

    internal static string ResolveApiKeySuffix(string apiKey) =>
        apiKey.Length <= 2 ? apiKey : apiKey[^2..];

    internal static int ResolveRemainingQuota(int quotaUsed, int quotaHits)
    {
        if (quotaHits > 0)
        {
            return 0;
        }

        return Math.Max(0, YouTubeQuotaCosts.DailyLimitPerKey - quotaUsed);
    }

    private static string? ResolveCapacityHint(int callsAttempted, int quotaHits)
    {
        if (callsAttempted == 0)
        {
            return "unused";
        }

        return quotaHits == 0 ? "spare-capacity-candidate" : "quota-exhausted";
    }

    private sealed class MutableKeyStats
    {
        public required string DisplayName { get; init; }
        public required string Project { get; init; }
        public required string Usage { get; init; }
        public int CallsAttempted;
        public int QuotaHits;
        public int QuotaUsed;
    }
}
