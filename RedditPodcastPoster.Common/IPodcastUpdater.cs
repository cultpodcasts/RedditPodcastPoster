using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

public interface IPodcastUpdater
{
    Task Update(Podcast podcast, DateTime? releasedSince, bool skipYouTubeUrlResolving);
}