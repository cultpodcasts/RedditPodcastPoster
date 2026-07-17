using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Factories;

public static class FindSpotifyEpisodeRequestFactory
{
    public static FindSpotifyEpisodeRequest Create(Podcast? podcast, PodcastServiceSearchCriteria criteria)
    {
        var criteriaFromYouTube = criteria.SourceAuthority == Service.YouTube;
        var release = criteria.Release;
        if (podcast != null)
        {
            release = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(
                podcast,
                criteria.Release,
                criteriaFromYouTube);
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
            criteria.Duration,
            EnrichingYouTubeDiscoveredEpisode: criteriaFromYouTube);
    }

    public static FindSpotifyEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        var enrichingYouTubeDiscoveredEpisode = EpisodeHasYouTubeIdentity(episode);
        var release = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);

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
            podcast.SpotifyMarket,
            enrichingYouTubeDiscoveredEpisode);
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

    private static bool EpisodeHasYouTubeIdentity(Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube != null;
}
