using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Logs the start of each stage of one podcast's index run with the podcast id/name, elapsed ms since the podcast
/// started and how long the previous stage took. Lines are Information (not shipped) unless the podcast is traced
/// (<see cref="PodcastIndexingStageLogOptions.WarningPodcastIds"/>: every line at Warning, so the last line before a
/// stall names the call that hung) or the previous stage was slow
/// (<see cref="PodcastIndexingStageLogOptions.SlowStageWarningSeconds"/>). Keep the message prefix stable: App Insights
/// queries key off it (docs/indexing-app-insights-queries.md).
/// </summary>
public sealed class PodcastIndexingStageLog : IPodcastIndexingStageReporter
{
    public const string MessagePrefix = "IndexerPodcastStage";

    private readonly ILogger _logger;
    private readonly Guid _podcastId;
    private readonly string _podcastName;
    private readonly TimeProvider _timeProvider;
    private readonly long _startTimestamp;
    private readonly object _sync = new();
    private readonly bool _traced;
    private readonly TimeSpan? _slowStage;
    private long _stageStartTimestamp;

    public PodcastIndexingStageLog(
        Guid podcastId,
        string podcastName,
        ILogger logger,
        TimeProvider timeProvider,
        PodcastIndexingStageLogOptions? options = null)
    {
        options ??= new PodcastIndexingStageLogOptions();
        _traced = options.IsWarningPodcast(podcastId);
        _slowStage = options.SlowStageWarning;
        _podcastId = podcastId;
        _podcastName = podcastName;
        _logger = logger;
        _timeProvider = timeProvider;
        _startTimestamp = timeProvider.GetTimestamp();
        _stageStartTimestamp = _startTimestamp;
    }

    public string CurrentStage { get; private set; } = "none";

    /// <summary>True when this podcast is on the Warning allowlist (every stage line shipped).</summary>
    public bool Traced => _traced;

    public string? CurrentStageDetail { get; private set; }

    public long ElapsedMilliseconds => (long) _timeProvider.GetElapsedTime(_startTimestamp).TotalMilliseconds;

    public void Stage(string stage, string? detail = null)
    {
        string previousStage;
        long previousStageMs;
        long elapsedMs;
        lock (_sync)
        {
            var now = _timeProvider.GetTimestamp();
            previousStage = CurrentStage;
            previousStageMs = (long) _timeProvider.GetElapsedTime(_stageStartTimestamp, now).TotalMilliseconds;
            elapsedMs = (long) _timeProvider.GetElapsedTime(_startTimestamp, now).TotalMilliseconds;
            CurrentStage = stage;
            CurrentStageDetail = detail;
            _stageStartTimestamp = now;
        }

        var slow = _slowStage.HasValue && previousStageMs >= (long) _slowStage.Value.TotalMilliseconds;
        var level = _traced || slow ? LogLevel.Warning : LogLevel.Information;
        _logger.Log(level,
            MessagePrefix +
            " podcast-id='{PodcastId}' podcast-name='{PodcastName}' stage='{Stage}' detail='{Detail}' elapsed-ms='{ElapsedMs}' previous-stage='{PreviousStage}' previous-stage-ms='{PreviousStageMs}' traced='{Traced}' slow-previous-stage='{SlowPreviousStage}'",
            _podcastId, _podcastName, stage, detail ?? string.Empty, elapsedMs, previousStage, previousStageMs,
            _traced, slow);
    }
}
