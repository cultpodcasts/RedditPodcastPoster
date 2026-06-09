namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public static class YouTubeVideoDurationMatcher
{
    private static readonly TimeSpan LongFormEpisodeDuration = TimeSpan.FromMinutes(5);
    private const double MaxShortfallRatio = 0.05;

    public static bool HasDuration(TimeSpan? videoLength) =>
        videoLength.HasValue && videoLength.Value > TimeSpan.Zero;

    public static bool IsAcceptableDurationMatch(TimeSpan episodeLength, TimeSpan? videoLength)
    {
        if (!HasDuration(videoLength))
        {
            return false;
        }

        if (episodeLength < LongFormEpisodeDuration)
        {
            return true;
        }

        var minimumAcceptableTicks = (long)(episodeLength.Ticks * (1 - MaxShortfallRatio));
        return videoLength!.Value.Ticks >= minimumAcceptableTicks;
    }
}
