using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Spotify;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public static class PodcastExtensions
{
    public static FindSpotifyPodcastRequest ToFindSpotifyPodcastRequest(this Podcast podcast)
    {
        return new FindSpotifyPodcastRequest(podcast.SpotifyId, podcast.Name,
            podcast.Episodes.Select(episode =>
                new FindSpotifyPodcastRequestEpisodes(episode.Release, episode.Urls.Spotify, episode.Title)).ToList());
    }
}