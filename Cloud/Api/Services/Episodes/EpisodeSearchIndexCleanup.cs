using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace Api.Services.Episodes;

public class EpisodeSearchIndexCleanup(
    SearchClient searchClient,
    ILogger<EpisodeSearchIndexCleanup> logger)
{
    public async Task DeleteSearchEntry(
        string podcastName,
        Guid episodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await searchClient.DeleteDocumentsAsync(
                "id",
                [episodeId.ToString()],
                new IndexDocumentsOptions { ThrowOnAnyError = true },
                cancellationToken);
            var success = result.Value.Results.First<IndexingResult>().Succeeded;
            if (!success)
            {
                logger.LogError(result.Value.Results.First<IndexingResult>().ErrorMessage);
            }
            else
            {
                logger.LogInformation(
                    "Removed episode from podcast '{podcastName}' with episode-id '{episodeId}' from search-index.",
                    podcastName, episodeId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error removing episode from podcast '{podcastName}' with episode-id '{episodeId}' from search-index.",
                podcastName, episodeId);
        }
    }

    /// <summary>Azure Search accepts at most 1000 actions per batch.</summary>
    internal const int DeleteBatchSize = 1000;

    /// <summary>
    /// Deletes many documents by id, in batches, the way a podcast removal does
    /// (<c>ThrowOnAnyError = false</c>, log the failures). Deleting an id that is not in the index succeeds, so this is
    /// safe to retry. Returns false when any batch or document failed.
    /// </summary>
    public async Task<bool> DeleteSearchEntries(
        string parentName,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var success = true;
        foreach (var batch in ids.Select(id => id.ToString()).Chunk(DeleteBatchSize))
        {
            try
            {
                var result = await searchClient.DeleteDocumentsAsync(
                    "id", batch, new IndexDocumentsOptions { ThrowOnAnyError = false }, cancellationToken);
                var failed = result.Value.Results.Where(x => !x.Succeeded).Select(x => x.Key).ToArray();
                if (failed.Length > 0)
                {
                    success = false;
                    logger.LogError(
                        "Removed {successCount} documents of '{parentName}'. Failed to remove {failureCount} documents with ids: {documentIds}.",
                        result.Value.Results.Count - failed.Length, parentName, failed.Length,
                        string.Join(",", failed.Select(x => $"'{x}'")));
                }
                else
                {
                    logger.LogInformation("Removed {successCount} documents of '{parentName}' from search-index.",
                        result.Value.Results.Count, parentName);
                }
            }
            catch (Exception ex)
            {
                success = false;
                logger.LogError(ex, "Exception while deleting {count} documents of '{parentName}' from search-index.",
                    batch.Length, parentName);
            }
        }

        return success;
    }
}
