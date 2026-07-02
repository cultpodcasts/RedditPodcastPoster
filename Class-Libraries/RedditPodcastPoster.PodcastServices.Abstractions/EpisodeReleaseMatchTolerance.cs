using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public static class EpisodeReleaseMatchTolerance
{
    private static readonly TimeSpan SameReleaseThreshold = TimeSpan.FromHours(3);
    private static readonly TimeSpan YouTubePublishDelayMatchThreshold = TimeSpan.FromDays(1);

    public static bool EpisodesReleaseMatch(Podcast podcast, Episode existingEpisode, Episode episodeToMerge)
    {
        var referenceLength = existingEpisode.Length > TimeSpan.Zero
            ? existingEpisode.Length
            : episodeToMerge.Length;
        var toleranceTicks = GetToleranceTicks(podcast, referenceLength);

        if (Math.Abs((existingEpisode.Release - episodeToMerge.Release).Ticks) < toleranceTicks)
        {
            return true;
        }

        var delay = podcast.YouTubePublishingDelay();
        if (delay == TimeSpan.Zero)
        {
            return false;
        }

        var existingIsYouTube = HasYouTubeIdentity(existingEpisode);
        var incomingIsYouTube = HasYouTubeIdentity(episodeToMerge);
        if (existingIsYouTube == incomingIsYouTube)
        {
            return false;
        }

        if (!existingIsYouTube && incomingIsYouTube)
        {
            // PlaylistItemFinder / SearchResultFinder: expected YouTube publish = audio release + delay
            var expectedPublish = existingEpisode.Release.Add(delay);
            return Math.Abs((episodeToMerge.Release - expectedPublish).Ticks) <
                   YouTubePublishDelayMatchThreshold.Ticks;
        }

        if (existingIsYouTube && HasSpotifyIdentity(episodeToMerge) &&
            podcast.ReleaseAuthority == Service.YouTube)
        {
            // FindSpotifyEpisodeRequestFactory.CalculateRelativeRelease: audio release = stored release - delay
            var expectedAudioRelease = existingEpisode.Release - delay;
            return Math.Abs((episodeToMerge.Release - expectedAudioRelease).Ticks) < toleranceTicks;
        }

        return false;
    }

    public static DateTime GetAudioReleaseForPlatformLookup(Podcast podcast, Episode episode) =>
        GetAudioReleaseForPlatformLookup(podcast, episode.Release, HasYouTubeIdentity(episode));

    public static DateTime GetAudioReleaseForPlatformLookup(
        Podcast podcast,
        DateTime release,
        bool episodeHasYouTubeIdentity)
    {
        var delay = podcast.YouTubePublishingDelay();
        if (delay == TimeSpan.Zero)
        {
            return release;
        }

        if (podcast.ReleaseAuthority == Service.YouTube)
        {
            return release - delay;
        }

        if (episodeHasYouTubeIdentity && HasAudioPlatformConfigured(podcast))
        {
            return release - delay;
        }

        return release;
    }

    public static long GetToleranceTicks(Podcast podcast, TimeSpan episodeLength)
    {
        var delay = podcast.YouTubePublishingDelay();
        if (delay == TimeSpan.Zero)
        {
            return Constants.YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
        }

        if (delay.Ticks < 0)
        {
            return Math.Abs(delay.Ticks);
        }

        if (podcast.ReleaseAuthority == Service.YouTube)
        {
            var tolerance = TimeSpan.FromTicks(delay.Ticks * 2).Add(SameReleaseThreshold);
            if (episodeLength > TimeSpan.Zero)
            {
                tolerance = tolerance.Add(episodeLength);
            }

            return tolerance.Ticks;
        }

        return Constants.YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
    }

    public static long GetToleranceTicks(
        Podcast? podcast,
        TimeSpan episodeLength,
        TimeSpan? youTubePublishingDelay,
        Service? releaseAuthority)
    {
        if (podcast != null)
        {
            return GetToleranceTicks(podcast, episodeLength);
        }

        if (youTubePublishingDelay is not { } delay || delay == TimeSpan.Zero)
        {
            return Constants.YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
        }

        if (delay.Ticks < 0)
        {
            return Math.Abs(delay.Ticks);
        }

        if (releaseAuthority == Service.YouTube)
        {
            var tolerance = TimeSpan.FromTicks(delay.Ticks * 2).Add(SameReleaseThreshold);
            if (episodeLength > TimeSpan.Zero)
            {
                tolerance = tolerance.Add(episodeLength);
            }

            return tolerance.Ticks;
        }

        return Constants.YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
    }

    private static bool HasYouTubeIdentity(Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube != null;

    private static bool HasSpotifyIdentity(Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify != null;

    private static bool HasAudioPlatformConfigured(Podcast podcast) =>
        !string.IsNullOrWhiteSpace(podcast.SpotifyId) || podcast.AppleId != null;
}
