using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

public interface ICachedApplePodcastService : IFlushable
{
    Task<AppleEpisode?> SingleUseGetEpisode(ApplePodcastId podcastId, long episodeId,
        IndexingContext indexingContext);

    public Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext);

    public Task<AppleEpisode?> GetEpisode(ApplePodcastId podcastId, long episodeId, IndexingContext indexingContext);
}