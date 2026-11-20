namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Duration bands for <see cref="EpisodePlatformMatcher"/>. Same-platform compares stay tight
/// (one minute). YouTube↔audio pairs use a five-minute title-free cap, or — when the titles
/// already support the match — the greater of that cap and 12% of the longer length, so an
/// ad-stripped YouTube file can still attach (#1470) without opening a title-free 12% window
/// for two long episodes on the same day.
/// </summary>
public static class EpisodeDurationTolerance
{
    public static readonly TimeSpan SamePlatform = TimeSpan.FromMinutes(1);

    /// <summary>Title-free YouTube↔audio cap. Also the floor of the title-supported band.</summary>
    public static readonly TimeSpan CrossPlatform = TimeSpan.FromMinutes(5);

    public const double TitleSupportedCrossPlatformFraction = 0.12;

    public static TimeSpan ForYouTubeAudioPair(TimeSpan left, TimeSpan right, bool titleSupported) =>
        titleSupported ? TitleSupportedYouTubeAudio(left, right) : CrossPlatform;

    /// <summary>Greater of <see cref="CrossPlatform"/> and 12% of the longer length.</summary>
    public static TimeSpan TitleSupportedYouTubeAudio(TimeSpan left, TimeSpan right)
    {
        var longer = left > right ? left : right;
        var proportional = TimeSpan.FromTicks((long)(longer.Ticks * TitleSupportedCrossPlatformFraction));
        return proportional > CrossPlatform ? proportional : CrossPlatform;
    }
}
