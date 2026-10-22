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
    public async Task<bool> UploadAsync(
        IReadOnlyList<EpisodeSearchRecord> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return true;
        }

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
            logger.LogError(ex, "Failed to swap search documents after catalogue kind migrate.");
            return false;
        }
    }

    public async Task<bool> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return true;
        }

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
            logger.LogError(ex, "Failed to delete {Count} search documents.", ids.Count);
            return false;
        }
    }
}
