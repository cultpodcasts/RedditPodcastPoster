using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Episodes.Matching;

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
            EnrichingYouTubeDiscoveredEpisode: criteriaFromYouTube,
            EpisodeDescription: criteria.EpisodeDescription,
            DefaultSubject: podcast?.DefaultSubject,
            IgnoredSubjects: podcast?.IgnoredSubjects,
            Language: podcast?.Language);
    }

    public static FindSpotifyEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        var enrichingYouTubeDiscoveredEpisode =
            !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)) ||
            EpisodeServicePresence.HasUrl(episode, ServiceKeys.YouTube);
        var release = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);

        return new FindSpotifyEpisodeRequest(
            podcast.SpotifyId,
            podcast.Name.Trim(),
            EpisodeServicePresence.SpotifyEpisodeId(episode) ?? string.Empty,
            episode.Title.Trim(),
            release,
            podcast.HasExpensiveSpotifyEpisodesQuery(),
            podcast.YouTubePublishingDelay(),
            podcast.ReleaseAuthority,
            episode.Length,
            podcast.SpotifyMarket,
            enrichingYouTubeDiscoveredEpisode,
            episode.Description,
            podcast.DefaultSubject,
            podcast.IgnoredSubjects,
            EpisodeLanguageResolution.ForEpisode(episode));
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
