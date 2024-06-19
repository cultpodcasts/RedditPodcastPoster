using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class CachedApplePodcastService(
    IApplePodcastService applePodcastService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CachedApplePodcastService> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICachedApplePodcastService
{
    private static readonly ConcurrentDictionary<string, IEnumerable<AppleEpisode>?> EpisodesCache = new();

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var cacheKey = GetCacheKey(podcastId.PodcastId, indexingContext.ReleasedSince);
        if (!EpisodesCache.TryGetValue(cacheKey, out var podcastEpisodes))
        {
            podcastEpisodes = EpisodesCache[cacheKey] =
                await applePodcastService.GetEpisodes(podcastId, indexingContext);
        }

        return podcastEpisodes;
    }

    public async Task<AppleEpisode?> GetEpisode(ApplePodcastId podcastId, long episodeId,
        IndexingContext indexingContext)
    {
        var cacheKey = GetCacheKey(podcastId.PodcastId, indexingContext.ReleasedSince);
        if (EpisodesCache.TryGetValue(cacheKey, out var podcastEpisodes))
        {
            var episode = podcastEpisodes?.SingleOrDefault(x => x.Id == episodeId);
            if (episode != null)
            {
                return episode;
            }
        }

        return await applePodcastService.GetEpisode(podcastId, episodeId, indexingContext);
    }

    public void Flush()
    {
        EpisodesCache.Clear();
    }

    private string GetCacheKey(long podcastId, DateTime? releasedSince)
    {
        if (releasedSince.HasValue)
        {
            return $"{podcastId}-{releasedSince.Value.Ticks}";
        }

        return $"{podcastId}";
    }
}