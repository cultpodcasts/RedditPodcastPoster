using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify.Factories;

public static class FindSpotifyEpisodeRequestFactory
{
    public static FindSpotifyEpisodeRequest Create(Podcast? podcast, PodcastServiceSearchCriteria criteria)
    {
        return new FindSpotifyEpisodeRequest(
            podcast?.SpotifyId ?? string.Empty,
            (podcast?.Name ?? criteria.ShowName).Trim(),
            string.Empty,
            criteria.EpisodeTitle.Trim(),
            criteria.Release,
            podcast?.HasExpensiveSpotifyEpisodesQuery() ?? true,
            podcast?.YouTubePublishingDelay()??TimeSpan.Zero,
            podcast?.ReleaseAuthority,
            criteria.Duration);
    }

    public static FindSpotifyEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        return new FindSpotifyEpisodeRequest(
            podcast.SpotifyId,
            podcast.Name.Trim(),
            episode.SpotifyId,
            episode.Title.Trim(),
            episode.Release,
            podcast.HasExpensiveSpotifyEpisodesQuery(),
            podcast.YouTubePublishingDelay(),
            podcast.ReleaseAuthority,
            episode.Length,
            podcast.SpotifyMarket);
    }

    public static FindSpotifyEpisodeRequest Create(string episodeSpotifyId)
    {
        return new FindSpotifyEpisodeRequest(
            string.Empty,
            string.Empty,
            episodeSpotifyId,
            string.Empty,
            null,
            true);
    }
}