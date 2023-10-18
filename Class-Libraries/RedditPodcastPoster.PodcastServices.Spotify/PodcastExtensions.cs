using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public static class PodcastExtensions
{
    public static FindSpotifyPodcastRequest ToFindSpotifyPodcastRequest(this Podcast podcast)
    {
        return new FindSpotifyPodcastRequest(podcast.SpotifyId, podcast.Name,
            Enumerable.ToList<FindSpotifyPodcastRequestEpisodes>(podcast.Episodes.Select(episode =>
                new FindSpotifyPodcastRequestEpisodes(episode.Release, episode.Urls.Spotify, episode.Title))));
    }
}