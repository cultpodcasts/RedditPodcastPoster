using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Search.Models;

namespace Api.Services.Catalogue;

/// <summary>
/// The immediate search step of a rename. It is the counterpart of the podcast rename's
/// <c>IndexEpisodes(episodeIds)</c>: the podcast step re-pushes whole episode documents. There is no push indexer for
/// news reports, TV episodes or films; the sibling pull indexers re-read them by <c>_ts</c>. So this patches only the
/// renamed field (<c>seriesName</c> for TV and news, <c>title</c> for a film) on documents already in the index, with an
/// Azure Search merge, in batches. A 404 means the document is not indexed (for example it is removed), so it is
/// skipped. The save also bumped <c>_ts</c>, so the next indexer run writes the same value anyway. That makes this safe
/// to retry.
/// </summary>
public class PlayableSearchRename(SearchClient searchClient, ILogger<PlayableSearchRename> logger)
{
    internal const string SeriesNameField = "seriesName";
    internal const string TitleField = "title";

    /// <summary>Azure Search accepts at most 1000 actions per batch.</summary>
    internal const int MergeBatchSize = 1000;

    public async Task<EntitySearchIndexerResponse> SetField(
        string field,
        string value,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new EntitySearchIndexerResponse { EpisodeIndexRequestState = EpisodeIndexRequestState.NoDocuments };
        }

        try
        {
            var failed = 0;
            foreach (var batch in ids.Chunk(MergeBatchSize))
            {
                var documents = batch.Select(id => new Azure.Search.Documents.Models.SearchDocument { ["id"] = id.ToString(), [field] = value });
                var result = await searchClient.MergeDocumentsAsync(
                    documents, new IndexDocumentsOptions { ThrowOnAnyError = false }, cancellationToken);
                foreach (var failure in result.Value.Results.Where(x => !x.Succeeded && x.Status != 404))
                {
                    failed++;
                    logger.LogError("Failed to set '{field}' on search document '{key}': {message}", field,
                        failure.Key, failure.ErrorMessage);
                }
            }

            return new EntitySearchIndexerResponse
                { IndexerState = failed == 0 ? IndexerState.Executed : IndexerState.Failure };
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Failed to set '{field}' on {count} search document(s). Status-code: {status}.", field,
                ids.Count, ex.Status);
            return new EntitySearchIndexerResponse
            {
                IndexerState = ex.Status == 429 ? IndexerState.TooManyRequests : IndexerState.Failure
            };
        }
    }
}
