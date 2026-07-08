using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes;

public static class EpisodeReleaseTolerance
{
    public static readonly TimeSpan SameReleaseThreshold = TimeSpan.FromHours(3);
    public static readonly TimeSpan YouTubePublishDelayMatchThreshold = TimeSpan.FromDays(1);

    /// <summary>
    /// High-confidence signal: incoming YouTube publish is within the delay-alignment window of audio release + offset.
    /// </summary>
    public static bool IsYouTubePublishDelayAligned(
        DateTime audioRelease,
        DateTime youTubeRelease,
        TimeSpan publishingDelay)
    {
        if (publishingDelay == TimeSpan.Zero)
        {
            return false;
        }

        var expectedPublish = audioRelease.Add(publishingDelay);
        return Math.Abs((youTubeRelease - expectedPublish).Ticks) < YouTubePublishDelayMatchThreshold.Ticks;
    }

    /// <summary>
    /// Moderate-confidence signal: audio and YouTube publishes share a calendar day (offset informs expectation, not a hard gate).
    /// </summary>
    public static bool AreCrossPlatformReleasesOnSameCalendarDay(
        DateTime audioRelease,
        DateTime youTubeRelease) =>
        DateOnly.FromDateTime(audioRelease) == DateOnly.FromDateTime(youTubeRelease);

    public const int YouTubeReleaseAuthoritySpotifyCatalogueDayTolerance = 5;
    public static readonly TimeSpan YouTubeAuthorityToAudioReleaseConsiderationThreshold = TimeSpan.FromDays(14);
    private static readonly TimeSpan YouTubeReleaseAuthorityEnrichmentLookahead = TimeSpan.FromDays(14);

    public static long GetToleranceTicks(Podcast podcast, TimeSpan episodeLength)
    {
        var delay = podcast.YouTubePublishingDelay();
        if (delay == TimeSpan.Zero)
        {
            return YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
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

        return YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
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
            return YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
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

        return YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
    }

    public static int GetSpotifyCatalogueDayTolerance(Podcast? podcast) =>
        podcast is { ReleaseAuthority: Service.YouTube } && podcast.YouTubePublishingDelay().Ticks < 0
            ? YouTubeReleaseAuthoritySpotifyCatalogueDayTolerance
            : 1;

    public static bool SpotifyCatalogueReleaseMatches(DateTime spotifyCatalogueRelease, DateTime expectedRelease) =>
        SpotifyCatalogueReleaseMatches(spotifyCatalogueRelease, expectedRelease, toleranceTicks: 0, podcast: null);

    public static bool SpotifyCatalogueReleaseMatches(
        DateTime spotifyCatalogueRelease,
        DateTime expectedRelease,
        long toleranceTicks) =>
        SpotifyCatalogueReleaseMatches(spotifyCatalogueRelease, expectedRelease, toleranceTicks, podcast: null);

    public static bool SpotifyCatalogueReleaseMatches(
        DateTime spotifyCatalogueRelease,
        DateTime expectedRelease,
        long toleranceTicks,
        Podcast? podcast)
    {
        var dayTolerance = GetSpotifyCatalogueDayTolerance(podcast);
        var spotifyDate = DateOnly.FromDateTime(spotifyCatalogueRelease);
        var expectedDate = DateOnly.FromDateTime(expectedRelease);
        if (Math.Abs(expectedDate.DayNumber - spotifyDate.DayNumber) <= dayTolerance)
        {
            return true;
        }

        return Math.Abs((spotifyCatalogueRelease - expectedRelease).Ticks) < toleranceTicks;
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

    public static bool ShouldPreserveYouTubeAuthoritativeRelease(Podcast podcast, Episode episode) =>
        podcast.ReleaseAuthority == Service.YouTube && HasYouTubeIdentity(episode);

    public static bool ShouldEnrichDespiteReleaseWindow(Episode episode, Podcast podcast)
    {
        var delay = podcast.YouTubePublishingDelay();
        if (podcast.ReleaseAuthority != Service.YouTube || delay.Ticks >= 0)
        {
            return false;
        }

        if (!HasYouTubeIdentity(episode) || !EpisodeMissingConfiguredPlatformIds(episode, podcast))
        {
            return false;
        }

        var expectedAudioRelease = GetAudioReleaseForPlatformLookup(podcast, episode);
        var windowStart = expectedAudioRelease.AddDays(-YouTubeReleaseAuthoritySpotifyCatalogueDayTolerance);
        var windowEnd = expectedAudioRelease.Add(YouTubeReleaseAuthorityEnrichmentLookahead);
        var now = DateTime.UtcNow;
        return now >= windowStart && now <= windowEnd;
    }

    private static bool EpisodeMissingConfiguredPlatformIds(Episode episode, Podcast podcast)
    {
        var needsSpotify = !string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
                           string.IsNullOrWhiteSpace(episode.SpotifyId);
        var needsApple = podcast.AppleId is > 0 && episode.AppleId is null or 0;
        return needsSpotify || needsApple;
    }

    private static bool HasYouTubeIdentity(Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube != null;

    private static bool HasSpotifyIdentity(Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify != null;

    private static bool HasAudioPlatformConfigured(Podcast podcast) =>
        !string.IsNullOrWhiteSpace(podcast.SpotifyId) || podcast.AppleId != null;
}
