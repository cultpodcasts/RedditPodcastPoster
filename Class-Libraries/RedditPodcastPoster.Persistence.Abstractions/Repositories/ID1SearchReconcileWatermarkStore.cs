using RedditPodcastPoster.Models.Search;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>Reads and saves the <see cref="D1SearchReconcileWatermark"/> LookUps singleton.</summary>
public interface ID1SearchReconcileWatermarkStore
{
    /// <returns>The saved watermark, or null when none has been saved yet.</returns>
    Task<D1SearchReconcileWatermark?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(D1SearchReconcileWatermark watermark, CancellationToken cancellationToken = default);
}
