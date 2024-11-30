using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface ICachedApplePodcastService : IApplePodcastService, IFlushable
{
    Task<AppleEpisode?> SingleUseGetEpisode(ApplePodcastId podcastId, long episodeId,
        IndexingContext indexingContext);
}