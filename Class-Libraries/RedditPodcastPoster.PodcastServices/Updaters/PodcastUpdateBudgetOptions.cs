namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Per-podcast time budget for the indexer's batch loop (<see cref="PodcastsUpdater"/>). A backstop so one podcast
/// whose calls stall cannot consume the whole activity: when the budget elapses the podcast's
/// <c>IndexingContext.CancellationToken</c> is cancelled, the podcast and the stage it reached are logged, and the
/// loop moves on to the next podcast. Bound from <see cref="SectionName"/> (e.g. app setting
/// <c>podcastUpdateBudget__PerPodcastBudgetSeconds</c>).
/// </summary>
public class PodcastUpdateBudgetOptions
{
    public const string SectionName = "podcastUpdateBudget";

    public const int DefaultPerPodcastBudgetSeconds = 60;

    public const int DefaultAbandonGraceSeconds = 15;

    /// <summary>Seconds one podcast's update may run before it is cancelled. Zero or less disables the budget.</summary>
    public int PerPodcastBudgetSeconds { get; set; } = DefaultPerPodcastBudgetSeconds;

    /// <summary>
    /// Seconds to wait after cancelling for the update to observe cancellation. A call that ignores its token is then
    /// abandoned (left to finish in the background) so the loop still moves on.
    /// </summary>
    public int AbandonGraceSeconds { get; set; } = DefaultAbandonGraceSeconds;

    public TimeSpan? PerPodcastBudget =>
        PerPodcastBudgetSeconds > 0 ? TimeSpan.FromSeconds(PerPodcastBudgetSeconds) : null;

    public TimeSpan AbandonGrace => TimeSpan.FromSeconds(Math.Max(0, AbandonGraceSeconds));
}
