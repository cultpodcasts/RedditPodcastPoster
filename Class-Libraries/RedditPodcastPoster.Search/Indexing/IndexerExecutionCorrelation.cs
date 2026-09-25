namespace RedditPodcastPoster.Search.Indexing;

/// <summary>
/// One indexer execution the monitor may correlate to a trigger.
/// <see cref="EndTime"/> is recorded so callers can show an end after the minimum
/// without that end making the row eligible.
/// </summary>
public readonly record struct IndexerExecutionCandidate(DateTimeOffset? StartTime, DateTimeOffset? EndTime);

/// <summary>
/// Picks the latest indexer execution whose start is at least <c>minRunStartUtc</c>.
/// A finished quota batch whose end is after the next trigger is not that run.
/// </summary>
public static class IndexerExecutionCorrelation
{
    /// <summary>
    /// Index of the winning candidate, or null when every candidate is ineligible.
    /// Candidates are considered in list order. An equal start does not replace an earlier winner.
    /// </summary>
    public static int? LatestIndex(
        DateTimeOffset? minRunStartUtc,
        IReadOnlyList<IndexerExecutionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        int? latest = null;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (minRunStartUtc.HasValue &&
                (!candidate.StartTime.HasValue || candidate.StartTime.Value < minRunStartUtc.Value))
            {
                continue;
            }

            if (latest is null ||
                (candidate.StartTime ?? DateTimeOffset.MinValue) >
                (candidates[latest.Value].StartTime ?? DateTimeOffset.MinValue))
            {
                latest = i;
            }
        }

        return latest;
    }
}
