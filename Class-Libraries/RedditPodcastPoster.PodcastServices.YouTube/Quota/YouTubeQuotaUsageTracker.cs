using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public sealed class YouTubeQuotaUsageTracker(
    IOptions<YouTubeSettings> settings,
    ILookupRepository lookupRepository,
    ILogger<YouTubeQuotaUsageTracker> logger) : IYouTubeQuotaUsageTracker
{
    private const string SourceApplication = "Indexer";

    private readonly YouTubeSettings _settings =
        settings.Value ?? throw new ArgumentNullException($"Missing {nameof(YouTubeSettings)}.");

    private readonly ConcurrentDictionary<string, MutableKeyStats> _stats = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sync = new(1, 1);
    private bool _hydrated;
    private DateOnly? _pacificQuotaDate;

    public Task RecordCallAsync(
        Application application,
        ApplicationUsage usage,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            application,
            usage,
            stats => stats.CallsAttempted++,
            cancellationToken);

    public Task RecordQuotaHitAsync(
        Application application,
        ApplicationUsage usage,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            application,
            usage,
            stats => stats.QuotaHits++,
            cancellationToken);

    public Task RecordQuotaConsumedAsync(
        Application application,
        ApplicationUsage usage,
        int quotaUnits,
        CancellationToken cancellationToken = default)
    {
        if (quotaUnits <= 0)
        {
            return Task.CompletedTask;
        }

        return MutateAsync(
            application,
            usage,
            stats => stats.QuotaUsed += quotaUnits,
            cancellationToken);
    }

    public async Task<YouTubeQuotaDailyReport> CreateReportAsync(
        DateOnly reportDate,
        string sourceApplication,
        CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await EnsureHydratedLockedAsync(cancellationToken);
            return BuildReport(reportDate, sourceApplication);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task EnsureHydratedAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await EnsureHydratedLockedAsync(cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task FlushToCosmosAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await EnsureHydratedLockedAsync(cancellationToken);
            await PersistLockedAsync(cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            _stats.Clear();
            _hydrated = false;
            _pacificQuotaDate = null;

            var emptyState = new YouTubeQuotaUsageState
            {
                PacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow),
                SourceApplication = SourceApplication,
                UpdatedUtc = DateTime.UtcNow,
                Entries = []
            };
            await lookupRepository.SaveYouTubeQuotaUsageState(emptyState);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task MutateAsync(
        Application application,
        ApplicationUsage usage,
        Action<MutableKeyStats> mutate,
        CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await EnsureHydratedLockedAsync(cancellationToken);
            mutate(GetOrAdd(application, usage));
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task EnsureHydratedLockedAsync(CancellationToken cancellationToken)
    {
        var currentPacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);
        if (_hydrated && _pacificQuotaDate == currentPacificQuotaDate)
        {
            return;
        }

        _stats.Clear();
        var savedState = await lookupRepository.GetYouTubeQuotaUsageState();
        if (savedState != null && savedState.PacificQuotaDate == currentPacificQuotaDate)
        {
            foreach (var entry in savedState.Entries)
            {
                _stats[entry.StatsKey] = new MutableKeyStats
                {
                    StatsKey = entry.StatsKey,
                    DisplayName = entry.DisplayName,
                    Project = entry.Project,
                    Usage = entry.Usage,
                    CallsAttempted = entry.CallsAttempted,
                    QuotaHits = entry.QuotaHits,
                    QuotaUsed = entry.QuotaUsed
                };
            }

            logger.LogDebug(
                "Hydrated YouTube quota usage tracker with {EntryCount} keys for Pacific quota day {PacificQuotaDate}.",
                savedState.Entries.Count,
                currentPacificQuotaDate);
        }
        else if (savedState != null)
        {
            logger.LogInformation(
                "Skipping stale YouTube quota usage state from Pacific quota day {SavedPacificQuotaDate}; current is {CurrentPacificQuotaDate}.",
                savedState.PacificQuotaDate,
                currentPacificQuotaDate);
        }

        _pacificQuotaDate = currentPacificQuotaDate;
        _hydrated = true;
    }

    private async Task PersistLockedAsync(CancellationToken cancellationToken)
    {
        var pacificQuotaDate = _pacificQuotaDate ?? YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);
        var state = new YouTubeQuotaUsageState
        {
            PacificQuotaDate = pacificQuotaDate,
            SourceApplication = SourceApplication,
            UpdatedUtc = DateTime.UtcNow,
            Entries = _stats.Values
                .Select(CreateUsageEntry)
                .OrderBy(x => x.Usage, StringComparer.Ordinal)
                .ThenBy(x => x.Project, StringComparer.Ordinal)
                .ThenBy(x => x.DisplayName, StringComparer.Ordinal)
                .ToList()
        };

        await lookupRepository.SaveYouTubeQuotaUsageState(state);
    }

    private YouTubeQuotaDailyReport BuildReport(DateOnly reportDate, string sourceApplication)
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
        IndexerKeyRingBuilder.GetFlatIndexerApplications(_settings.Applications);

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
            HourPrimary = ResolveRingOrder(application),
            Reattempt = null,
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

    private static YouTubeQuotaUsageEntry CreateUsageEntry(MutableKeyStats stats) =>
        new()
        {
            StatsKey = stats.StatsKey,
            DisplayName = stats.DisplayName,
            Project = stats.Project,
            Usage = stats.Usage,
            CallsAttempted = stats.CallsAttempted,
            QuotaHits = stats.QuotaHits,
            QuotaUsed = stats.QuotaUsed
        };

    private MutableKeyStats GetOrAdd(Application application, ApplicationUsage usage)
    {
        var key = CreateStatsKey(application.ApiKey, usage);
        return _stats.GetOrAdd(key, _ => new MutableKeyStats
        {
            DisplayName = application.DisplayName,
            Project = application.Name,
            Usage = usage.ToString(),
            StatsKey = key
        });
    }

    internal static string CreateStatsKey(string apiKey, ApplicationUsage usage) => $"{usage}:{apiKey}";

    internal static int ResolveRingOrder(Application application)
    {
        var parts = application.DisplayName.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3
            && parts[0] == "Indexer"
            && parts[1] == "Key"
            && int.TryParse(parts[2], out var ringOrder))
        {
            return ringOrder;
        }

        throw new InvalidOperationException(
            $"Unable to resolve ring order from indexer display name '{application.DisplayName}'.");
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
        public required string StatsKey { get; init; }
        public required string DisplayName { get; init; }
        public required string Project { get; init; }
        public required string Usage { get; init; }
        public int CallsAttempted;
        public int QuotaHits;
        public int QuotaUsed;
    }
}
