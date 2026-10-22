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

    public const int DefaultExpensivePassBudgetMultiplier = 3;

    /// <summary>Seconds one podcast's update may run before it is cancelled. Zero or less disables the budget.</summary>
    public int PerPodcastBudgetSeconds { get; set; } = DefaultPerPodcastBudgetSeconds;

    /// <summary>
    /// Seconds to wait after cancelling for the update to observe cancellation. A call that ignores its token is then
    /// abandoned (left to finish in the background) so the loop still moves on.
    /// </summary>
    public int AbandonGraceSeconds { get; set; } = DefaultAbandonGraceSeconds;

    /// <summary>
    /// Multiplies the budget on a pass that allows expensive YouTube or Spotify queries (the 00 UTC primary pass), where
    /// a podcast may legitimately walk a whole catalogue. Zero or less disables the budget on those passes.
    /// </summary>
    public int ExpensivePassBudgetMultiplier { get; set; } = DefaultExpensivePassBudgetMultiplier;

    public TimeSpan? PerPodcastBudget =>
        PerPodcastBudgetSeconds > 0 ? TimeSpan.FromSeconds(PerPodcastBudgetSeconds) : null;

    /// <summary>The budget for a pass: <see cref="PerPodcastBudget"/>, scaled on a pass that allows expensive queries.</summary>
    public TimeSpan? BudgetFor(bool expensivePass)
    {
        if (!expensivePass || PerPodcastBudget is not { } budget)
        {
            return PerPodcastBudget;
        }

        return ExpensivePassBudgetMultiplier > 0 ? budget * ExpensivePassBudgetMultiplier : null;
    }

    public TimeSpan AbandonGrace => TimeSpan.FromSeconds(Math.Max(0, AbandonGraceSeconds));
}
