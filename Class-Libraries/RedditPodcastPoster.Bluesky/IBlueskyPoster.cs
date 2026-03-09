using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPoster
{
    Task<BlueskySendStatus> Post(PodcastEpisodeV2 podcastEpisode, Uri? shortUrl);
}