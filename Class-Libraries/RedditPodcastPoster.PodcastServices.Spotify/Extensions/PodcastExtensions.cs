using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Extensions;

public static class PodcastExtensions
{
    public static FindSpotifyPodcastRequest ToFindSpotifyPodcastRequest(this Podcast podcast, IEnumerable<Episode> episodes)
    {
        return new FindSpotifyPodcastRequest(
            podcast.SpotifyId, 
            podcast.Name,
            episodes.Select(episode =>
                new FindSpotifyPodcastRequestEpisodes(episode.Release, episode.Urls.Spotify, episode.Title)).ToList());
    }
}
