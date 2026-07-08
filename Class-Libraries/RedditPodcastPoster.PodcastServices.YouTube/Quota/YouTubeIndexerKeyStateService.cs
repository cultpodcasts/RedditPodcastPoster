using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public sealed class YouTubeIndexerKeyStateService(
    IYouTubeIndexerKeyStateStore indexerKeyStateStore,
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
    ILogger<YouTubeIndexerKeyStateService> logger) : IYouTubeIndexerKeyStateService
{
    public async Task<IndexerKeyRingSessionStart> ResolveSessionStartAsync(CancellationToken cancellationToken = default)
    {
        var hourFallback = youTubeApiKeyStrategy.GetApplication(ApplicationUsage.Indexer);
        var hourFallbackRingIndex = hourFallback.Index;
        var ring = youTubeApiKeyStrategy.BuildIndexerKeyRing(0);
        var currentPacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);

        var savedState = await indexerKeyStateStore.GetAsync(cancellationToken);
        var initialRingIndex = IndexerKeyRingSessionResolver.ResolveInitialRingIndex(
            ring,
            savedState,
            currentPacificQuotaDate,
            hourFallbackRingIndex);

        if (savedState == null)
        {
            logger.LogInformation(
                "No saved YouTube indexer key state found. Starting at hour fallback ring index {HourFallbackRingIndex}, ring index {InitialRingIndex}.",
                hourFallbackRingIndex,
                initialRingIndex);
        }
        else if (savedState.PacificQuotaDate != currentPacificQuotaDate)
        {
            logger.LogInformation(
                "Saved YouTube indexer key state is from quota day {SavedPacificQuotaDate}; current is {CurrentPacificQuotaDate}. Resetting to hour fallback ring index {HourFallbackRingIndex}, ring index {InitialRingIndex}.",
                savedState.PacificQuotaDate,
                currentPacificQuotaDate,
                hourFallbackRingIndex,
                initialRingIndex);
        }
        else if (initialRingIndex == hourFallbackRingIndex &&
                 !string.IsNullOrWhiteSpace(savedState.LastApiKey))
        {
            logger.LogWarning(
                "Saved YouTube indexer key '{LastApiKeyEnding}' is no longer in the configured ring. Falling back to hour fallback ring index {HourFallbackRingIndex}.",
                savedState.LastApiKey[^Math.Min(2, savedState.LastApiKey.Length)..],
                hourFallbackRingIndex);
        }
        else
        {
            logger.LogInformation(
                "Resuming YouTube indexer key ring at index {InitialRingIndex} (hour fallback ring index {HourFallbackRingIndex}) for quota day {PacificQuotaDate}.",
                initialRingIndex,
                hourFallbackRingIndex,
                currentPacificQuotaDate);
        }

        return new IndexerKeyRingSessionStart(hourFallbackRingIndex, initialRingIndex, ring);
    }

    public async Task PersistSessionEndAsync(int ringIndex, string apiKey, CancellationToken cancellationToken = default)
    {
        var state = new YouTubeIndexerKeyState
        {
            PacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow),
            LastRingIndex = ringIndex,
            LastApiKey = apiKey,
            UpdatedUtc = DateTime.UtcNow
        };

        await indexerKeyStateStore.SaveAsync(state, cancellationToken);

        logger.LogInformation(
            "Persisted YouTube indexer key state at ring index {RingIndex} for quota day {PacificQuotaDate}.",
            ringIndex,
            state.PacificQuotaDate);
    }
}
