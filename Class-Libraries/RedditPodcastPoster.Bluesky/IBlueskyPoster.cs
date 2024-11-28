using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPoster
{
    Task<BlueskySendStatus> Post(PodcastEpisode podcastEpisode, Uri? shortUrl);
}