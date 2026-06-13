using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public static class EpisodeReleaseMatchTolerance
{
    private static readonly TimeSpan SameReleaseThreshold = TimeSpan.FromHours(3);

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
}
