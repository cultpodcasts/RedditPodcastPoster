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

        var delay = podcast.YouTubePublishingDelay();
        if (delay.Ticks >= 0 &&
            Math.Abs((existingEpisode.Release - episodeToMerge.Release).Ticks) < toleranceTicks)
        {
            return true;
        }

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

        if (incomingIsYouTube && HasSpotifyIdentity(existingEpisode) &&
            podcast.ReleaseAuthority == Service.YouTube)
        {
            var expectedYouTubeRelease = existingEpisode.Release.Add(delay);
            return Math.Abs((episodeToMerge.Release - expectedYouTubeRelease).Ticks) <
                   YouTubePublishDelayMatchThreshold.Ticks;
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

    public static bool SpotifyCatalogueReleaseMatches(DateTime spotifyCatalogueRelease, DateTime expectedRelease)
    {
        var spotifyDate = DateOnly.FromDateTime(spotifyCatalogueRelease);
        var expectedDate = DateOnly.FromDateTime(expectedRelease);
        return Math.Abs(expectedDate.DayNumber - spotifyDate.DayNumber) <= 1;
    }

    public static bool SpotifyCatalogueReleaseMatches(
        DateTime spotifyCatalogueRelease,
        DateTime expectedRelease,
        long toleranceTicks)
    {
        if (SpotifyCatalogueReleaseMatches(spotifyCatalogueRelease, expectedRelease))
        {
            return true;
        }

        return Math.Abs((spotifyCatalogueRelease - expectedRelease).Ticks) < toleranceTicks;
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
            return YouTubePublishDelayMatchThreshold.Add(SameReleaseThreshold).Ticks;
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
            return YouTubePublishDelayMatchThreshold.Add(SameReleaseThreshold).Ticks;
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
