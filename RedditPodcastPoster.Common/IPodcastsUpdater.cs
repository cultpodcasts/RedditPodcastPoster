namespace RedditPodcastPoster.Common;

public interface IPodcastsUpdater
{
    Task UpdatePodcasts(IndexOptions indexOptions);
}