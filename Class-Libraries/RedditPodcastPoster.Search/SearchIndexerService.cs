using System.Net;
using Azure;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.Search;

public enum IndexerState
{
    Unknown = 0,

    Executed,
    Failure,
    TooManyRequests,
    AlreadyRunning
}

public class SearchIndexerService(
    SearchIndexerClient searchIndexerClient,
    IOptions<SearchIndexConfig> searchIndexConfig,
    ILogger<SearchIndexerService> logger)
    : ISearchIndexerService
{
    private readonly SearchIndexConfig _searchIndexConfig = searchIndexConfig.Value;

    public async Task<IndexerState> RunIndexer()
    {
        logger.LogInformation($"Indexing '{_searchIndexConfig.IndexerName}'.");
        try
        {
            var response = await searchIndexerClient.RunIndexerAsync(_searchIndexConfig.IndexerName);
            if (response.Status != (int) HttpStatusCode.Accepted)
            {
                logger.LogError(
                    $"Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{response.Status}' and reason '{response.ReasonPhrase}'.");
                return IndexerState.Failure;
            }

            logger.LogInformation($"Ran indexer '{_searchIndexConfig.IndexerName}'.");
            return IndexerState.Executed;
        }
        catch (RequestFailedException ex)
        {
            switch (ex.Status)
            {
                case (int) HttpStatusCode.TooManyRequests:
                    logger.LogError(
                        $"Too Many Requests. Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{ex.Status}'.");
                    return IndexerState.TooManyRequests;
                case (int) HttpStatusCode.Conflict:
                    logger.LogError(
                        $"Indexer already running. Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{ex.Status}'.");
                    return IndexerState.AlreadyRunning;
                default:
                    logger.LogError(ex,
                        $"Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{ex.Status}' and message '{ex.Message}'.");
                    return IndexerState.Failure;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to run indexer '{_searchIndexConfig.IndexerName}' with message '{ex.Message}'.");
            return IndexerState.Failure;
        }
    }
}