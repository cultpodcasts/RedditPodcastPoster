using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Logs each stage of one podcast's index run at Warning (the level the indexer ships for RedditPodcastPoster
/// categories), with the podcast id/name, elapsed ms since the podcast started and how long the previous stage took.
/// The last <c>IndexerPodcastStage</c> line before a stall names the call that hung. Keep the message prefix stable:
/// App Insights queries key off it (docs/indexing-app-insights-queries.md).
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
    private long _stageStartTimestamp;

    public PodcastIndexingStageLog(Guid podcastId, string podcastName, ILogger logger, TimeProvider timeProvider)
    {
        _podcastId = podcastId;
        _podcastName = podcastName;
        _logger = logger;
        _timeProvider = timeProvider;
        _startTimestamp = timeProvider.GetTimestamp();
        _stageStartTimestamp = _startTimestamp;
    }

    public string CurrentStage { get; private set; } = "none";

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

        _logger.LogWarning(
            MessagePrefix +
            " podcast-id='{PodcastId}' podcast-name='{PodcastName}' stage='{Stage}' detail='{Detail}' elapsed-ms='{ElapsedMs}' previous-stage='{PreviousStage}' previous-stage-ms='{PreviousStageMs}'",
            _podcastId, _podcastName, stage, detail ?? string.Empty, elapsedMs, previousStage, previousStageMs);
    }
}
