using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Bluesky;

public interface IBlueskyPoster
{
    Task<BlueskySendStatus> Post(PodcastEpisode podcastEpisode, Uri? shortUrl);
}
