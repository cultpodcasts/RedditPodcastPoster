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

    /// <summary>
    /// Longest budget a curator's on-demand index (one HTTP request) may have. With
    /// <see cref="MaxOnDemandAbandonGraceSeconds"/> and <see cref="MaxOnDemandAbandonDrainSeconds"/> the worst case is
    /// 150 + 15 + 30 = 195 seconds, under the Azure App Service front end's ~230 second request timeout, so the
    /// curator gets the timed-out answer instead of a gateway error while the work carries on.
    /// </summary>
    public const int MaxOnDemandBudgetSeconds = 150;

    public const int DefaultOnDemandBudgetSeconds = MaxOnDemandBudgetSeconds;

    /// <summary>Upper bound on the grace period an on-demand index waits for its update to observe cancellation.</summary>
    public const int MaxOnDemandAbandonGraceSeconds = 15;

    /// <summary>Upper bound on how long an on-demand index waits for an abandoned update to settle before it answers.</summary>
    public const int MaxOnDemandAbandonDrainSeconds = 30;

    public const int DefaultOnDemandAbandonDrainSeconds = MaxOnDemandAbandonDrainSeconds;

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

    /// <summary>
    /// Seconds a curator's on-demand index of one TV show or news organisation may run before it is cancelled. It is
    /// independent of <see cref="PerPodcastBudgetSeconds"/> and <see cref="ExpensivePassBudgetMultiplier"/> (the curator
    /// context always allows expensive queries), so an HTTP request always has a cap. Clamped to at most
    /// <see cref="MaxOnDemandBudgetSeconds"/>; zero or less means the default.
    /// </summary>
    public int OnDemandBudgetSeconds { get; set; } = DefaultOnDemandBudgetSeconds;

    /// <summary>
    /// Seconds an on-demand index waits, after abandoning an update that ignored its cancelled token, for that update to
    /// settle before answering. The request's DI scope (repositories, HTTP clients, the pass API cache) is disposed once
    /// the response is sent, so the update should stop while they are still alive. Clamped to
    /// 0..<see cref="MaxOnDemandAbandonDrainSeconds"/>.
    /// </summary>
    public int OnDemandAbandonDrainSeconds { get; set; } = DefaultOnDemandAbandonDrainSeconds;

    /// <summary>The on-demand budget: <see cref="OnDemandBudgetSeconds"/>, defaulted and clamped (never unbounded).</summary>
    public TimeSpan OnDemandBudget => TimeSpan.FromSeconds(
        OnDemandBudgetSeconds <= 0 ? DefaultOnDemandBudgetSeconds : Math.Min(OnDemandBudgetSeconds, MaxOnDemandBudgetSeconds));

    /// <summary>The on-demand grace: <see cref="AbandonGrace"/>, capped at <see cref="MaxOnDemandAbandonGraceSeconds"/>.</summary>
    public TimeSpan OnDemandAbandonGrace =>
        TimeSpan.FromSeconds(Math.Clamp(AbandonGraceSeconds, 0, MaxOnDemandAbandonGraceSeconds));

    /// <summary>The on-demand drain: <see cref="OnDemandAbandonDrainSeconds"/>, clamped.</summary>
    public TimeSpan OnDemandAbandonDrain =>
        TimeSpan.FromSeconds(Math.Clamp(OnDemandAbandonDrainSeconds, 0, MaxOnDemandAbandonDrainSeconds));
}
