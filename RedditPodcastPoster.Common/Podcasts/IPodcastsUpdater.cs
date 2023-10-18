using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastsUpdater
{
    Task<bool> UpdatePodcasts(IndexingContext indexingContext);
}