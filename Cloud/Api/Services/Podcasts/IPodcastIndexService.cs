using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastIndexService
{
    /// <param name="backlog">
    /// When true, walk the full catalogue (<c>ReleasedSince</c> unset). Default false keeps the curator lookback
    /// (<c>indexer__ReleasedDaysAgo</c>).
    /// </param>
    Task<PodcastIndexResult> IndexAsync(
        string podcastName,
        CancellationToken cancellationToken,
        bool backlog = false);
}
