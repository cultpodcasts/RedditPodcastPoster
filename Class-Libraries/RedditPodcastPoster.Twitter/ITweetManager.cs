using RedditPodcastPoster.Models;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITweetManager
{
    Task<RemoveTweetState> RemoveTweet(PodcastEpisode podcastEpisode);
}