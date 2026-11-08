using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Services;

public interface IPlayableSearchDocumentSwap
{
    Task<bool> UploadAsync(IReadOnlyList<EpisodeSearchRecord> documents, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes documents by id and reports whether every delete succeeded. Deleting an id that is not in the index
    /// succeeds, so a retry is safe. Used when a playable stops being a playable (attached to a film as a trailer).
    /// </summary>
    Task<bool> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}

public class PlayableSearchDocumentSwap(
    SearchClient searchClient,
    ILogger<PlayableSearchDocumentSwap> logger) : IPlayableSearchDocumentSwap
{
    /// <summary>
    /// Azure AI Search accepts at most 1000 documents per indexing request, and a whole podcast transfer can exceed
    /// that, so documents go in batches of this size. A failed batch does not stop the rest.
    /// </summary>
    public const int MaxDocumentsPerBatch = 500;

    public async Task<bool> UploadAsync(
        IReadOnlyList<EpisodeSearchRecord> documents,
        CancellationToken cancellationToken)
    {
        var allSucceeded = true;
        foreach (var batch in documents.Chunk(MaxDocumentsPerBatch))
        {
            allSucceeded &= await UploadBatch(batch, cancellationToken);
        }

        return allSucceeded;
    }

    private async Task<bool> UploadBatch(EpisodeSearchRecord[] documents, CancellationToken cancellationToken)
    {
        try
        {
            var result = await searchClient.MergeOrUploadDocumentsAsync(
                documents,
                new IndexDocumentsOptions { ThrowOnAnyError = false },
                cancellationToken);
            var failures = result.Value.Results.Where(x => !x.Succeeded).ToArray();
            foreach (var failure in failures)
            {
                logger.LogError(
                    "Failed to swap search document '{Key}': {ErrorMessage}",
                    failure.Key,
                    failure.ErrorMessage);
            }

            return failures.Length == 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to swap {Count} search documents after catalogue kind migrate.",
                documents.Length);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var allSucceeded = true;
        foreach (var batch in ids.Chunk(MaxDocumentsPerBatch))
        {
            allSucceeded &= await DeleteBatch(batch, cancellationToken);
        }

        return allSucceeded;
    }

    private async Task<bool> DeleteBatch(Guid[] ids, CancellationToken cancellationToken)
    {
        try
        {
            var result = await searchClient.DeleteDocumentsAsync(
                "id",
                ids.Select(id => id.ToString()),
                new IndexDocumentsOptions { ThrowOnAnyError = false },
                cancellationToken);
            var failures = result.Value.Results.Where(x => !x.Succeeded).ToArray();
            foreach (var failure in failures)
            {
                logger.LogError(
                    "Failed to delete search document '{Key}': {ErrorMessage}",
                    failure.Key,
                    failure.ErrorMessage);
            }

            return failures.Length == 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete {Count} search documents.", ids.Length);
            return false;
        }
    }
}
