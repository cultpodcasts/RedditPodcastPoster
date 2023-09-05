namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastsUpdater
{
    Task UpdatePodcasts(IndexOptions indexOptions);
}