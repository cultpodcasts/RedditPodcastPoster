namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Controls the level of <see cref="PodcastIndexingStageLog"/> lines. Stage lines are Information (not shipped by the
/// indexer, which ships RedditPodcastPoster categories at Warning) so they cost no App Insights ingestion by default.
/// They are promoted to Warning (shipped) only when the podcast is on <see cref="WarningPodcastIds"/> (every stage,
/// including each stage's start, so a call that never returns is still named) or when the stage that just ended took at
/// least <see cref="SlowStageWarningSeconds"/>. Bound from <see cref="SectionName"/>, e.g. app settings
/// <c>indexerStageLog__WarningPodcastIds</c> and <c>indexerStageLog__SlowStageWarningSeconds</c>.
/// </summary>
public class PodcastIndexingStageLogOptions
{
    public const string SectionName = "indexerStageLog";

    public const int DefaultSlowStageWarningSeconds = 10;

    /// <summary>Shortest accepted id prefix, so a typo cannot match a large share of podcasts.</summary>
    public const int MinimumIdPrefixLength = 8;

    /// <summary>
    /// Comma- or semicolon-separated podcast ids (or id prefixes of at least <see cref="MinimumIdPrefixLength"/>
    /// characters) whose every stage is logged at Warning. Empty by default.
    /// </summary>
    public string? WarningPodcastIds { get; set; }

    /// <summary>A stage that took at least this many seconds is logged at Warning. Zero or less disables it.</summary>
    public int SlowStageWarningSeconds { get; set; } = DefaultSlowStageWarningSeconds;

    public TimeSpan? SlowStageWarning =>
        SlowStageWarningSeconds > 0 ? TimeSpan.FromSeconds(SlowStageWarningSeconds) : null;

    public bool IsWarningPodcast(Guid podcastId)
    {
        if (string.IsNullOrWhiteSpace(WarningPodcastIds))
        {
            return false;
        }

        var id = podcastId.ToString("D");
        return WarningPodcastIds
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => entry.Length >= MinimumIdPrefixLength)
            .Any(entry => id.StartsWith(entry, StringComparison.OrdinalIgnoreCase));
    }
}
