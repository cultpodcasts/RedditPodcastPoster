using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Factories;

public static class FindSpotifyEpisodeRequestFactory
{
    public static FindSpotifyEpisodeRequest Create(Podcast? podcast, PodcastServiceSearchCriteria criteria)
    {
        var release = criteria.Release;
        if (podcast != null)
        {
            release = CalculateRelativeRelease(podcast, criteria.Release);
        }

        return new FindSpotifyEpisodeRequest(
            podcast?.SpotifyId ?? string.Empty,
            (podcast?.Name ?? criteria.ShowName).Trim(),
            string.Empty,
            criteria.EpisodeTitle.Trim(),
            release,
            podcast?.HasExpensiveSpotifyEpisodesQuery() ?? true,
            podcast?.YouTubePublishingDelay() ?? TimeSpan.Zero,
            podcast?.ReleaseAuthority,
            criteria.Duration);
    }

    public static FindSpotifyEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        var release = CalculateRelativeRelease(podcast, episode.Release);

        return new FindSpotifyEpisodeRequest(
            podcast.SpotifyId,
            podcast.Name.Trim(),
            episode.SpotifyId,
            episode.Title.Trim(),
            release,
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

    private static DateTime CalculateRelativeRelease(Podcast podcast, DateTime release)
    {
        if (podcast.ReleaseAuthority == Service.YouTube && podcast.YouTubePublishingDelay() != TimeSpan.Zero)
        {
            release -= podcast.YouTubePublishingDelay();
        }

        return release;
    }
}