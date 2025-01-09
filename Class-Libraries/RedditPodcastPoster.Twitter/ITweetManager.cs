using RedditPodcastPoster.Models;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITweetManager
{
    Task<RemoveTweetsState> RemoveTweet(PodcastEpisode podcastEpisode);
}