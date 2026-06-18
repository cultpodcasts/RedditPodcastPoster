using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public sealed class YouTubeIndexerKeyStateService(
    ILookupRepository lookupRepository,
    IYouTubeApiKeyStrategy youTubeApiKeyStrategy,
    ILogger<YouTubeIndexerKeyStateService> logger) : IYouTubeIndexerKeyStateService
{
    public async Task<IndexerKeyRingSessionStart> ResolveSessionStartAsync(CancellationToken cancellationToken = default)
    {
        var hourPrimary = youTubeApiKeyStrategy.GetApplication(ApplicationUsage.Indexer);
        var startPrimaryIndex = hourPrimary.Index;
        var ring = youTubeApiKeyStrategy.BuildIndexerKeyRing(startPrimaryIndex);
        var currentPacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);

        var savedState = await lookupRepository.GetYouTubeIndexerKeyState();
        var initialRingIndex = IndexerKeyRingSessionResolver.ResolveInitialRingIndex(
            ring,
            savedState,
            currentPacificQuotaDate);

        if (savedState == null)
        {
            logger.LogInformation(
                "No saved YouTube indexer key state found. Starting at hour primary index {StartPrimaryIndex}, ring index {InitialRingIndex}.",
                startPrimaryIndex,
                initialRingIndex);
        }
        else if (savedState.PacificQuotaDate != currentPacificQuotaDate)
        {
            logger.LogInformation(
                "Saved YouTube indexer key state is from quota day {SavedPacificQuotaDate}; current is {CurrentPacificQuotaDate}. Resetting to hour primary index {StartPrimaryIndex}, ring index {InitialRingIndex}.",
                savedState.PacificQuotaDate,
                currentPacificQuotaDate,
                startPrimaryIndex,
                initialRingIndex);
        }
        else if (initialRingIndex == 0 &&
                 !string.IsNullOrWhiteSpace(savedState.LastApiKey))
        {
            logger.LogWarning(
                "Saved YouTube indexer key '{LastApiKeyEnding}' is no longer in the configured ring. Falling back to hour primary index {StartPrimaryIndex}, ring index 0.",
                savedState.LastApiKey[^Math.Min(2, savedState.LastApiKey.Length)..],
                startPrimaryIndex);
        }
        else
        {
            logger.LogInformation(
                "Resuming YouTube indexer key ring at index {InitialRingIndex} (hour primary index {StartPrimaryIndex}) for quota day {PacificQuotaDate}.",
                initialRingIndex,
                startPrimaryIndex,
                currentPacificQuotaDate);
        }

        return new IndexerKeyRingSessionStart(startPrimaryIndex, initialRingIndex, ring);
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

        await lookupRepository.SaveYouTubeIndexerKeyState(state);

        logger.LogInformation(
            "Persisted YouTube indexer key state at ring index {RingIndex} for quota day {PacificQuotaDate}.",
            ringIndex,
            state.PacificQuotaDate);
    }
}
