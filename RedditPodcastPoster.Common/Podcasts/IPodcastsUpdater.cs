namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastsUpdater
{
    Task<IndexPodcastsResult> UpdatePodcasts(IndexingContext indexingContext);
}