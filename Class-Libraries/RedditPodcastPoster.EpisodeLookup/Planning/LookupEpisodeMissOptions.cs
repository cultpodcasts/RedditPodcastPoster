namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// Index-only miss memory: skip a Spotify/Apple episode lookup while the catalogue fingerprint is unchanged
/// and the last full attempt is within <see cref="MissRetryAfter"/> (#1558).
/// </summary>
public sealed class LookupEpisodeMissOptions
{
    /// <summary>When false, Index always runs the full plan and never records or skips misses.</summary>
    public bool MissMemoryEnabled { get; set; } = true;

    /// <summary>How long a remembered miss may skip lookups while the fingerprint is unchanged (default 24 h).</summary>
    public TimeSpan MissRetryAfter { get; set; } = TimeSpan.FromHours(24);

    public static bool IsValid(LookupEpisodeMissOptions options) =>
        options.MissRetryAfter >= TimeSpan.FromSeconds(1) &&
        options.MissRetryAfter <= TimeSpan.FromDays(7);
}
