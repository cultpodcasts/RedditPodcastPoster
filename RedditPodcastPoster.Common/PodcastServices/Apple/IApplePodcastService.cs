namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IApplePodcastService
{
    public Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext);
}