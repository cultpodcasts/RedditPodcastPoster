using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public sealed class YouTubeQuotaUsageTracker(
    IOptions<YouTubeSettings> settings,
    IYouTubeQuotaUsageStateStore quotaUsageStateStore,
    ILogger<YouTubeQuotaUsageTracker> logger) : IYouTubeQuotaUsageTracker
{
    private const string SourceApplication = "Indexer";

    private readonly YouTubeSettings _settings =
        settings.Value ?? throw new ArgumentNullException($"Missing {nameof(YouTubeSettings)}.");

    private readonly ConcurrentDictionary<string, MutableKeyStats> _stats = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sync = new(1, 1);
    private bool _hydrated;
    private DateOnly? _pacificQuotaDate;
    private int _podcastsNotIndexedDueToQuota;
    private int _podcastsNotEnrichedDueToQuota;
    private int _ringExhaustionCount;
    private int _nonQuotaErrorCount;

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
        YouTubeQuotaOperation operation,
        CancellationToken cancellationToken = default)
    {
        var quotaUnits = YouTubeQuotaCosts.GetCost(operation);
        return MutateAsync(
            application,
            usage,
            stats =>
            {
                stats.QuotaHits++;
                stats.QuotaUsed += quotaUnits;
                AddOperationUsage(stats.QuotaConsumedByOperation, operation, quotaUnits);
            },
            cancellationToken);
    }

    public Task RecordQuotaConsumedAsync(
        Application application,
        ApplicationUsage usage,
        YouTubeQuotaOperation operation,
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
            stats =>
            {
                stats.QuotaUsed += quotaUnits;
                AddOperationUsage(stats.QuotaConsumedByOperation, operation, quotaUnits);
            },
            cancellationToken);
    }

    public Task RecordRingExhaustionAsync(CancellationToken cancellationToken = default) =>
        MutateReportCounterAsync(() => _ringExhaustionCount++, cancellationToken);

    public Task RecordNonQuotaErrorAsync(CancellationToken cancellationToken = default) =>
        MutateReportCounterAsync(() => _nonQuotaErrorCount++, cancellationToken);

    public Task RecordPodcastNotIndexedDueToQuotaAsync(CancellationToken cancellationToken = default) =>
        MutateReportCounterAsync(() => _podcastsNotIndexedDueToQuota++, cancellationToken);

    public Task RecordPodcastNotEnrichedDueToQuotaAsync(CancellationToken cancellationToken = default) =>
        MutateReportCounterAsync(() => _podcastsNotEnrichedDueToQuota++, cancellationToken);

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
            _podcastsNotIndexedDueToQuota = 0;
            _podcastsNotEnrichedDueToQuota = 0;
            _ringExhaustionCount = 0;
            _nonQuotaErrorCount = 0;

            var emptyState = new YouTubeQuotaUsageState
            {
                PacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow),
                SourceApplication = SourceApplication,
                UpdatedUtc = DateTime.UtcNow,
                Entries = [],
                PodcastsNotIndexedDueToQuota = 0,
                PodcastsNotEnrichedDueToQuota = 0,
                RingExhaustionCount = 0,
                NonQuotaErrorCount = 0
            };
            await quotaUsageStateStore.SaveAsync(emptyState);
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

    private async Task MutateReportCounterAsync(Action mutate, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await EnsureHydratedLockedAsync(cancellationToken);
            mutate();
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
        var savedState = await quotaUsageStateStore.GetAsync(cancellationToken);
        if (savedState != null && savedState.PacificQuotaDate == currentPacificQuotaDate)
        {
            foreach (var entry in savedState.Entries)
            {
                var operationUsage = new MutableOperationUsage();
                CopyOperationUsage(operationUsage, entry.QuotaConsumedByOperation);
                _stats[entry.StatsKey] = new MutableKeyStats
                {
                    StatsKey = entry.StatsKey,
                    DisplayName = entry.DisplayName,
                    Project = entry.Project,
                    Usage = entry.Usage,
                    CallsAttempted = entry.CallsAttempted,
                    QuotaHits = entry.QuotaHits,
                    QuotaUsed = entry.QuotaUsed,
                    QuotaConsumedByOperation = operationUsage
                };
            }

            _podcastsNotIndexedDueToQuota = savedState.PodcastsNotIndexedDueToQuota;
            _podcastsNotEnrichedDueToQuota = savedState.PodcastsNotEnrichedDueToQuota;
            _ringExhaustionCount = savedState.RingExhaustionCount;
            _nonQuotaErrorCount = savedState.NonQuotaErrorCount;

            logger.LogDebug(
                "Hydrated YouTube quota usage tracker with {EntryCount} keys for Pacific quota day {PacificQuotaDate}.",
                savedState.Entries.Count,
                currentPacificQuotaDate);
        }
        else
        {
            if (savedState != null)
            {
                logger.LogInformation(
                    "Skipping stale YouTube quota usage state from Pacific quota day {SavedPacificQuotaDate}; current is {CurrentPacificQuotaDate}.",
                    savedState.PacificQuotaDate,
                    currentPacificQuotaDate);
            }

            _podcastsNotIndexedDueToQuota = 0;
            _podcastsNotEnrichedDueToQuota = 0;
            _ringExhaustionCount = 0;
            _nonQuotaErrorCount = 0;
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
                .ToList(),
            PodcastsNotIndexedDueToQuota = _podcastsNotIndexedDueToQuota,
            PodcastsNotEnrichedDueToQuota = _podcastsNotEnrichedDueToQuota,
            RingExhaustionCount = _ringExhaustionCount,
            NonQuotaErrorCount = _nonQuotaErrorCount
        };

        await quotaUsageStateStore.SaveAsync(state, cancellationToken);
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
            UnusedIndexerKeys = unusedIndexerKeys,
            PodcastsNotIndexedDueToQuota = _podcastsNotIndexedDueToQuota,
            PodcastsNotEnrichedDueToQuota = _podcastsNotEnrichedDueToQuota,
            RingExhaustionCount = _ringExhaustionCount,
            NonQuotaErrorCount = _nonQuotaErrorCount
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
        var operationUsage = stats?.QuotaConsumedByOperation ?? new MutableOperationUsage();

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
            EstimatedQuotaUsed = ResolveEstimatedQuotaUsed(quotaUsed, quotaHits),
            DailyLimit = YouTubeQuotaCosts.DailyLimitPerKey,
            QuotaConsumedByOperation = CreateOperationUsageReport(operationUsage)
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
            EstimatedQuotaUsed = ResolveEstimatedQuotaUsed(stats.QuotaUsed, stats.QuotaHits),
            DailyLimit = YouTubeQuotaCosts.DailyLimitPerKey,
            QuotaConsumedByOperation = CreateOperationUsageReport(stats.QuotaConsumedByOperation),
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
            QuotaUsed = stats.QuotaUsed,
            QuotaConsumedByOperation = CreateOperationUsageReport(stats.QuotaConsumedByOperation)
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

    internal static int ResolveEstimatedQuotaUsed(int quotaUsed, int quotaHits) =>
        quotaHits > 0 ? Math.Max(quotaUsed, YouTubeQuotaCosts.DailyLimitPerKey) : quotaUsed;

    private static string? ResolveCapacityHint(int callsAttempted, int quotaHits)
    {
        if (callsAttempted == 0)
        {
            return "unused";
        }

        return quotaHits == 0 ? "spare-capacity-candidate" : "quota-exhausted";
    }

    private static void AddOperationUsage(MutableOperationUsage usage, YouTubeQuotaOperation operation, int quotaUnits)
    {
        switch (operation)
        {
            case YouTubeQuotaOperation.SearchList:
                usage.SearchList += quotaUnits;
                break;
            case YouTubeQuotaOperation.ChannelsList:
                usage.ChannelsList += quotaUnits;
                break;
            case YouTubeQuotaOperation.PlaylistItemsList:
                usage.PlaylistItemsList += quotaUnits;
                break;
            case YouTubeQuotaOperation.PlaylistsList:
                usage.PlaylistsList += quotaUnits;
                break;
            case YouTubeQuotaOperation.VideosList:
                usage.VideosList += quotaUnits;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    private static YouTubeQuotaConsumedByOperation CreateOperationUsageReport(MutableOperationUsage usage) =>
        new()
        {
            SearchList = usage.SearchList,
            ChannelsList = usage.ChannelsList,
            PlaylistItemsList = usage.PlaylistItemsList,
            PlaylistsList = usage.PlaylistsList,
            VideosList = usage.VideosList
        };

    private static void CopyOperationUsage(MutableOperationUsage target, YouTubeQuotaConsumedByOperation source)
    {
        target.SearchList = source.SearchList;
        target.ChannelsList = source.ChannelsList;
        target.PlaylistItemsList = source.PlaylistItemsList;
        target.PlaylistsList = source.PlaylistsList;
        target.VideosList = source.VideosList;
    }

    private static MutableOperationUsage CloneOperationUsage(YouTubeQuotaConsumedByOperation usage)
    {
        var clone = new MutableOperationUsage();
        CopyOperationUsage(clone, usage);
        return clone;
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
        public MutableOperationUsage QuotaConsumedByOperation { get; init; } = new();
    }

    private sealed class MutableOperationUsage
    {
        public int SearchList;
        public int ChannelsList;
        public int PlaylistItemsList;
        public int PlaylistsList;
        public int VideosList;
    }
}
