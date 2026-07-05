using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes;

internal static class EpisodeReleaseTolerance
{
    internal static readonly TimeSpan SameReleaseThreshold = TimeSpan.FromHours(3);
    internal static readonly TimeSpan YouTubePublishDelayMatchThreshold = TimeSpan.FromDays(1);
    internal const int YouTubeReleaseAuthoritySpotifyCatalogueDayTolerance = 5;
    internal static readonly TimeSpan YouTubeAuthorityToAudioReleaseConsiderationThreshold = TimeSpan.FromDays(14);

    internal static long GetToleranceTicks(Podcast podcast, TimeSpan episodeLength)
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

    internal static int GetSpotifyCatalogueDayTolerance(Podcast? podcast) =>
        podcast is { ReleaseAuthority: Service.YouTube } && podcast.YouTubePublishingDelay().Ticks < 0
            ? YouTubeReleaseAuthoritySpotifyCatalogueDayTolerance
            : 1;

    internal static bool SpotifyCatalogueReleaseMatches(
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
}
