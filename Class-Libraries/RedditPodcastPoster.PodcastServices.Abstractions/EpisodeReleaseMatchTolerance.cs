using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public static class EpisodeReleaseMatchTolerance
{
    private static readonly TimeSpan SameReleaseThreshold = TimeSpan.FromHours(3);
    private static readonly TimeSpan YouTubePublishDelayMatchThreshold = TimeSpan.FromDays(1);
    private const int MembersFirstSpotifyCatalogueDayTolerance = 5;
    private static readonly TimeSpan MembersFirstEnrichmentLookahead = TimeSpan.FromDays(14);

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
            return SpotifyCatalogueReleaseMatches(
                episodeToMerge.Release,
                expectedAudioRelease,
                toleranceTicks,
                podcast);
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

    public static DateTime GetAudioReleaseForPlatformLookup(Podcast podcast, Episode episode)
    {
        if (podcast.ReleaseAuthority == Service.YouTube &&
            HasYouTubeIdentity(episode) &&
            HasAudioPlatformIdentity(episode))
        {
            // After Spotify/Apple merge, Release is the audio catalogue date — not YouTube publish.
            return episode.Release;
        }

        return GetAudioReleaseForPlatformLookup(podcast, episode.Release, HasYouTubeIdentity(episode));
    }

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
        var dayTolerance = GetSpotifyCatalogueDayTolerance(podcast: null);
        var spotifyDate = DateOnly.FromDateTime(spotifyCatalogueRelease);
        var expectedDate = DateOnly.FromDateTime(expectedRelease);
        return Math.Abs(expectedDate.DayNumber - spotifyDate.DayNumber) <= dayTolerance;
    }

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
        var windowStart = expectedAudioRelease.AddDays(-MembersFirstSpotifyCatalogueDayTolerance);
        var windowEnd = expectedAudioRelease.Add(MembersFirstEnrichmentLookahead);
        var now = DateTime.UtcNow;
        return now >= windowStart && now <= windowEnd;
    }

    private static int GetSpotifyCatalogueDayTolerance(Podcast? podcast) =>
        podcast is { ReleaseAuthority: Service.YouTube } && podcast.YouTubePublishingDelay().Ticks < 0
            ? MembersFirstSpotifyCatalogueDayTolerance
            : 1;

    private static bool EpisodeMissingConfiguredPlatformIds(Episode episode, Podcast podcast)
    {
        var needsSpotify = !string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
                           string.IsNullOrWhiteSpace(episode.SpotifyId);
        var needsApple = podcast.AppleId is > 0 && episode.AppleId is null or 0;
        return needsSpotify || needsApple;
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

    private static bool HasAudioPlatformIdentity(Episode episode) =>
        HasSpotifyIdentity(episode) || episode.AppleId is > 0 || episode.Urls.Apple != null;

    private static bool HasAudioPlatformConfigured(Podcast podcast) =>
        !string.IsNullOrWhiteSpace(podcast.SpotifyId) || podcast.AppleId != null;
}
