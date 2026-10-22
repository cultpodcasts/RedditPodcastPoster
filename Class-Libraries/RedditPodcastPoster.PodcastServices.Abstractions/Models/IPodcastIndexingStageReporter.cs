namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

/// <summary>
/// Records the stage a single podcast's index run has reached, so a run that stalls can be traced to the call
/// it was waiting on (the last stage logged before the stall). Carried on <see cref="IndexingContext.StageReporter"/>
/// for the duration of one podcast update; absent outside the indexer's per-podcast loop.
/// </summary>
public interface IPodcastIndexingStageReporter
{
    /// <summary>The most recently reported stage name.</summary>
    string CurrentStage { get; }

    /// <summary>The detail reported with <see cref="CurrentStage"/>, if any.</summary>
    string? CurrentStageDetail { get; }

    /// <summary>Milliseconds since the podcast's update started.</summary>
    long ElapsedMilliseconds { get; }

    /// <summary>Marks the start of <paramref name="stage"/> (logged with podcast id, name and elapsed ms).</summary>
    void Stage(string stage, string? detail = null);
}
