namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IAppleEpisodeResolver
{
    Task<AppleEpisode?> FindEpisode(FindAppleEpisodeRequest request, IndexingContext indexingContext);
}