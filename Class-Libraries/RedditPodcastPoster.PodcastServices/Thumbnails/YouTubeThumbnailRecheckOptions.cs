namespace RedditPodcastPoster.PodcastServices.Thumbnails;

/// <summary>
/// Settings for the delayed re-check of recently released YouTube episodes whose stored image is a
/// high-resolution tier YouTube can withdraw after indexing (<c>maxresdefault</c> / <c>hq720</c>).
/// Bound from <see cref="SectionName"/>; every value has a working default so the re-check runs
/// without app settings.
/// </summary>
public class YouTubeThumbnailRecheckOptions
{
    public const string SectionName = "youtubeThumbnailRecheck";

    /// <summary>How long after release a stored high-resolution image keeps being re-checked.</summary>
    public const int DefaultRecheckWindowHours = 48;

    /// <summary>Most stored images downloaded per run (one HTTP GET each, no YouTube quota).</summary>
    public const int DefaultMaxStoredImageProbesPerRun = 25;

    /// <summary>
    /// Most withdrawn images re-resolved per run. Each costs one YouTube videos.list call
    /// plus at most one probe per thumbnail candidate.
    /// </summary>
    public const int DefaultMaxReResolvesPerRun = 10;

    /// <summary>Per-probe timeout for one stored-image download. Must not exceed the HTTP client's own timeout.</summary>
    public const int DefaultProbeTimeoutSeconds = 5;

    /// <summary>Wall-clock budget for one run; episodes not reached are deferred to the next run.</summary>
    public const int DefaultRunBudgetSeconds = 60;

    public bool Enabled { get; set; } = true;

    public int ProbeTimeoutSeconds { get; set; } = DefaultProbeTimeoutSeconds;

    public int RunBudgetSeconds { get; set; } = DefaultRunBudgetSeconds;

    public int RecheckWindowHours { get; set; } = DefaultRecheckWindowHours;

    public int MaxStoredImageProbesPerRun { get; set; } = DefaultMaxStoredImageProbesPerRun;

    public int MaxReResolvesPerRun { get; set; } = DefaultMaxReResolvesPerRun;

    public override string ToString() =>
        $"{nameof(YouTubeThumbnailRecheckOptions)} {{{nameof(Enabled)}: '{Enabled}', " +
        $"{nameof(RecheckWindowHours)}: '{RecheckWindowHours}', " +
        $"{nameof(MaxStoredImageProbesPerRun)}: '{MaxStoredImageProbesPerRun}', " +
        $"{nameof(MaxReResolvesPerRun)}: '{MaxReResolvesPerRun}', " +
        $"{nameof(ProbeTimeoutSeconds)}: '{ProbeTimeoutSeconds}', " +
        $"{nameof(RunBudgetSeconds)}: '{RunBudgetSeconds}'}}.";
}
