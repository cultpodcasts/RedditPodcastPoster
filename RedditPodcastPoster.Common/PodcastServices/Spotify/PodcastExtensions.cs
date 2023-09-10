using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public static class PodcastExtensions
{
    public static SpotifyFindPodcastRequest ToSpotifyFindPodcastRequest(this Podcast podcast)
    {
        return new SpotifyFindPodcastRequest(podcast.SpotifyId, podcast.Name,
            podcast.Episodes.Select(episode =>
                new SpotifyFindPodcastRequestEpisodes(episode.Release, episode.Urls.Spotify, episode.Title)).ToList());
    }
}