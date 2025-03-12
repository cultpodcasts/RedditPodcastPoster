using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IApplePodcastService
{
    public Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext);

    public Task<AppleEpisode?> GetEpisode(long episodeId, IndexingContext indexingContext);
}