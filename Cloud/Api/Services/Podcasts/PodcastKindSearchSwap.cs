using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Search.Models;

namespace Api.Services.Podcasts;

public class PodcastKindSearchSwap(IPlayableSearchDocumentSwap inner) : IPodcastKindSearchSwap
{
    public Task<bool> UploadAsync(
        IReadOnlyList<EpisodeSearchRecord> documents,
        CancellationToken cancellationToken) =>
        inner.UploadAsync(documents, cancellationToken);
}
