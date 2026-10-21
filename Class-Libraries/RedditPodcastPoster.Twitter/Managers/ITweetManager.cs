using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Managers;

public interface ITweetManager
{
    Task<RemoveTweetState> RemoveTweet(PodcastEpisode podcastEpisode);

    /// <summary>Same matching as the podcast overload (parent name, length and release in the tweet text).</summary>
    Task<RemoveTweetState> RemoveTweet(SocialPostRemovalTarget target);
}
