namespace RedditPodcastPoster.Common;

public interface IPodcastsUpdater
{
    Task UpdatePodcasts(DateTime? releasedSince, bool skipYouTubeUrlResolving);
}