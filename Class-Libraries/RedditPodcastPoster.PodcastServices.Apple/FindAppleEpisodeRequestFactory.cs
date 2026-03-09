using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using Episode = RedditPodcastPoster.Models.V2.Episode;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;
using LegacyEpisode = RedditPodcastPoster.Models.Episode;
using LegacyPodcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.PodcastServices.Apple;

public static class FindAppleEpisodeRequestFactory
{
    public static FindAppleEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        var release = CalculateRelativeRelease(podcast, episode.Release);
        return new FindAppleEpisodeRequest(
            podcast.AppleId,
            podcast.Name,
            episode.AppleId,
            episode.Title,
            release,
            podcast.ReleaseAuthority,
            episode.Length,
            podcast.YouTubePublishingDelay()
        );
    }

    public static FindAppleEpisodeRequest Create(LegacyPodcast podcast, LegacyEpisode episode)
    {
        var release = CalculateRelativeRelease(podcast, episode.Release);
        return new FindAppleEpisodeRequest(
            podcast.AppleId,
            podcast.Name,
            episode.AppleId,
            episode.Title,
            release,
            podcast.ReleaseAuthority,
            episode.Length,
            podcast.YouTubePublishingDelay()
        );
    }

    private static DateTime CalculateRelativeRelease(Podcast podcast, DateTime release)
    {
        if (podcast.ReleaseAuthority == Service.YouTube && podcast.YouTubePublishingDelay() != TimeSpan.Zero)
        {
            release -= podcast.YouTubePublishingDelay();
        }

        return release;
    }

    private static DateTime CalculateRelativeRelease(LegacyPodcast podcast, DateTime release)
    {
        if (podcast.ReleaseAuthority == Service.YouTube && podcast.YouTubePublishingDelay() != TimeSpan.Zero)
        {
            release -= podcast.YouTubePublishingDelay();
        }

        return release;
    }

    public static FindAppleEpisodeRequest Create(
        Podcast? podcast,
        iTunesSearch.Library.Models.Podcast applePodcast,
        PodcastServiceSearchCriteria criteria)
    {
        var release = criteria.Release;
        if (podcast != null)
        {
            release = CalculateRelativeRelease(podcast, criteria.Release);
        }

        return new FindAppleEpisodeRequest(
            podcast?.AppleId ?? applePodcast.Id,
            applePodcast.Name,
            null,
            criteria.EpisodeTitle,
            release,
            podcast?.ReleaseAuthority,
            criteria.Duration,
            podcast?.YouTubePublishingDelay()??null);
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