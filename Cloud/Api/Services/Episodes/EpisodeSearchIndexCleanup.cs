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
}
