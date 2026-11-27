using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastIndexService
{
    /// <summary>Curator hub Index: keep <c>ReleasedSince</c> from <c>indexer__ReleasedDaysAgo</c>.</summary>
    Task<PodcastIndexResult> IndexAsync(string podcastName, CancellationToken cancellationToken);

    /// <summary>
    /// Curator cold-start / backlog Index: use the far-past <c>ReleasedSince</c> floor so providers walk essentially the full catalogue.
    /// </summary>
    Task<PodcastIndexResult> IndexBacklogAsync(string podcastName, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only dry run of <see cref="IndexBacklogAsync"/>: counts the episodes a backlog Index would add, with the same
    /// indexing context. Saves nothing and does not touch the search index.
    /// </summary>
    Task<PodcastIndexPreviewResult> PreviewBacklogAsync(string podcastName, CancellationToken cancellationToken);
}
