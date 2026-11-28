using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Indexing.Services;

public interface IIndexer
{
    Task<IndexResponse> Index(Guid podcastId, IndexingContext indexingContext, bool forceIndex = false);
    Task<IndexResponse> Index(string podcastName, IndexingContext indexingContext);

    /// <summary>
    /// Dry run of <see cref="Index(string, IndexingContext)"/>: resolves the podcast the same way, discovers what an
    /// index would add (short-episode and elimination rules applied) and counts it. Saves nothing and takes no
    /// indexing lease. Same on-demand time budget as an index (<see cref="IndexStatus.TimedOut"/>).
    /// </summary>
    Task<IndexPreviewResponse> Preview(string podcastName, IndexingContext indexingContext);
}
