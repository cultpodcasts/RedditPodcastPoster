using System.Collections.Concurrent;

namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Counts consecutive budget cuts per podcast for the life of the host process (singleton), so a podcast that is cut
/// on every pass shows a rising <c>consecutive-cuts</c> on its <c>IndexerPodcastBudgetExceeded</c> line. A completed
/// update resets the count. Process-local by design: a host restart resets it, and the App Insights query in
/// docs/indexing-app-insights-queries.md counts cuts per podcast across hosts.
/// </summary>
public class PodcastUpdateBudgetCutTracker
{
    private readonly ConcurrentDictionary<Guid, int> _consecutiveCuts = new();

    public int RecordCut(Guid podcastId) =>
        _consecutiveCuts.AddOrUpdate(podcastId, 1, (_, count) => count + 1);

    public void RecordCompleted(Guid podcastId) => _consecutiveCuts.TryRemove(podcastId, out _);
}
