using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public interface IYouTubeIndexerKeyStateService
{
    Task<IndexerKeyRingSessionStart> ResolveSessionStartAsync(CancellationToken cancellationToken = default);

    Task PersistSessionEndAsync(int ringIndex, string apiKey, CancellationToken cancellationToken = default);
}

internal static class IndexerKeyRingSessionResolver
{
    internal static int ResolveInitialRingIndex(
        IReadOnlyList<ApplicationWrapper> ring,
        YouTubeIndexerKeyState? savedState,
        DateOnly currentPacificQuotaDate,
        int hourFallbackRingIndex)
    {
        if (savedState == null ||
            savedState.PacificQuotaDate != currentPacificQuotaDate ||
            string.IsNullOrWhiteSpace(savedState.LastApiKey))
        {
            return hourFallbackRingIndex;
        }

        var indexByKey = FindRingIndexByApiKey(ring, savedState.LastApiKey);
        if (indexByKey.HasValue)
        {
            return indexByKey.Value;
        }

        if (savedState.LastRingIndex >= 0 &&
            savedState.LastRingIndex < ring.Count &&
            string.Equals(ring[savedState.LastRingIndex].Application.ApiKey, savedState.LastApiKey, StringComparison.Ordinal))
        {
            return savedState.LastRingIndex;
        }

        return hourFallbackRingIndex;
    }

    private static int? FindRingIndexByApiKey(IReadOnlyList<ApplicationWrapper> ring, string apiKey)
    {
        for (var index = 0; index < ring.Count; index++)
        {
            if (string.Equals(ring[index].Application.ApiKey, apiKey, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return null;
    }
}
