using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public static class FindSpotifyEpisodeRequestFactory
{
    public static FindSpotifyEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        return new FindSpotifyEpisodeRequest(
            podcast.SpotifyId,
            podcast.Name.Trim(),
            episode.SpotifyId,
            episode.Title.Trim(),
            podcast.HasExpensiveSpotifyEpisodesQuery());
    }

    public static FindSpotifyEpisodeRequest Create(string episodeSpotifyId)
    {
        return new FindSpotifyEpisodeRequest(
            string.Empty,
            string.Empty,
            episodeSpotifyId,
            string.Empty,
            true);
    }
}