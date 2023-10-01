namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastsUpdater
{
    Task<IndexPodcastsResult> UpdatePodcasts(IndexOptions indexOptions);
}