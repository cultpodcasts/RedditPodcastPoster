using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Factories;

public static class FindAppleEpisodeRequestFactory
{
    public static FindAppleEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        var release = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);
        var enrichingYouTubeDiscoveredEpisode =
            !string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube != null;
        return new FindAppleEpisodeRequest(
            podcast.AppleId,
            podcast.Name,
            episode.AppleId,
            episode.Title,
            release,
            podcast.ReleaseAuthority,
            episode.Length,
            podcast.YouTubePublishingDelay(),
            enrichingYouTubeDiscoveredEpisode
        );
    }

    public static FindAppleEpisodeRequest Create(
        Podcast? podcast,
        iTunesSearch.Library.Models.Podcast applePodcast,
        PodcastServiceSearchCriteria criteria)
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

        return new FindAppleEpisodeRequest(
            podcast?.AppleId ?? applePodcast.Id,
            applePodcast.Name,
            null,
            criteria.EpisodeTitle,
            release,
            podcast?.ReleaseAuthority,
            criteria.Duration,
            podcast?.YouTubePublishingDelay() ?? null,
            criteriaFromYouTube);
    }

    public static FindAppleEpisodeRequest Create(long podcastId, long episodeId)
    {
        return new FindAppleEpisodeRequest(
            podcastId,
            string.Empty,
            episodeId,
            string.Empty,
            null,
            null,
            null,
            null);
    }
}
