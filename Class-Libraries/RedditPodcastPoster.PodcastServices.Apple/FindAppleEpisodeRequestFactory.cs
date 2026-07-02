using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public static class FindAppleEpisodeRequestFactory
{
    public static FindAppleEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        var release = EpisodeReleaseMatchTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);
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
        var release = criteria.Release;
        if (podcast != null)
        {
            release = EpisodeReleaseMatchTolerance.GetAudioReleaseForPlatformLookup(podcast, criteria.Release, false);
        }

        return new FindAppleEpisodeRequest(
            podcast?.AppleId ?? applePodcast.Id,
            applePodcast.Name,
            null,
            criteria.EpisodeTitle,
            release,
            podcast?.ReleaseAuthority,
            criteria.Duration,
            podcast?.YouTubePublishingDelay() ?? null);
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
