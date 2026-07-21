using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Managers;

public interface ITweetManager
{
    Task<RemoveTweetState> RemoveTweet(PodcastEpisode podcastEpisode);
}
