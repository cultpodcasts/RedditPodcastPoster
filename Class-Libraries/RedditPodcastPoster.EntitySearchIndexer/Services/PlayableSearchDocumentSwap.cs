using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Services;

public interface IPlayableSearchDocumentSwap
{
    Task<bool> UploadAsync(IReadOnlyList<EpisodeSearchRecord> documents, CancellationToken cancellationToken);
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
}
