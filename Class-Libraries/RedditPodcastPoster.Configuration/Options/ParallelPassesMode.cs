namespace RedditPodcastPoster.Configuration.Options;

/// <summary>
/// Soak-test switch for running the hour's two Indexer activities side by side
/// (<c>indexer__ParallelPasses</c>). Absent / default is <see cref="Off"/>.
/// </summary>
public enum ParallelPassesMode
{
    /// <summary>Sequential passes (production default until soak completes).</summary>
    Off = 0,

    /// <summary>Parallel only on odd UTC hours (lighter baseline; even hours stay sequential as control).</summary>
    OddHours = 1,

    /// <summary>Parallel on every hour the mode allows (YouTube-enabled hours still stay sequential).</summary>
    All = 2
}
