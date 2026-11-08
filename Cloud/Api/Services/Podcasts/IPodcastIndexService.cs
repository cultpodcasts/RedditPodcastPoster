using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastIndexService
{
    /// <summary>Curator hub Index: keep <c>ReleasedSince</c> from <c>indexer__ReleasedDaysAgo</c>.</summary>
    Task<PodcastIndexResult> IndexAsync(string podcastName, CancellationToken cancellationToken);

    /// <summary>
    /// Curator cold-start / backlog Index: clear <c>ReleasedSince</c> so providers walk the full catalogue.
    /// </summary>
    Task<PodcastIndexResult> IndexBacklogAsync(string podcastName, CancellationToken cancellationToken);
}
