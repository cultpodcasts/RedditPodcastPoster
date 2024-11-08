using System.Net;
using Azure;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.Search;

public class SearchIndexerService(
    SearchIndexerClient searchIndexerClient,
    IOptions<SearchIndexConfig> searchIndexConfig,
    ILogger<SearchIndexerService> logger)
    : ISearchIndexerService
{
    private static readonly TimeSpan IndexingWaitPeriod = TimeSpan.FromMinutes(3);
    private readonly SearchIndexConfig _searchIndexConfig = searchIndexConfig.Value;

    public async Task<IndexerStateWrapper> RunIndexer()
    {
        logger.LogInformation($"Indexing '{_searchIndexConfig.IndexerName}'.");
        try
        {
            var response = await searchIndexerClient.RunIndexerAsync(_searchIndexConfig.IndexerName);
            if (response.Status != (int) HttpStatusCode.Accepted)
            {
                logger.LogError(
                    $"Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{response.Status}' and reason '{response.ReasonPhrase}'.");
                return new IndexerStateWrapper(IndexerState.Failure);
            }

            logger.LogInformation($"Ran indexer '{_searchIndexConfig.IndexerName}'.");
            return new IndexerStateWrapper(IndexerState.Executed);
        }
        catch (RequestFailedException ex)
        {
            var statusCode = (HttpStatusCode) ex.Status;
            switch (statusCode)
            {
                case HttpStatusCode.TooManyRequests:
                case HttpStatusCode.Conflict:
                {
                    TimeSpan? lastRan = null;
                    TimeSpan? nextRun = null;
                    var indexerStatus = await searchIndexerClient.GetIndexerStatusAsync(_searchIndexConfig.IndexerName);
                    if (indexerStatus.HasValue)
                    {
                        var lastEndTime = indexerStatus.Value.LastResult.EndTime;
                        if (lastEndTime.HasValue)
                        {
                            lastRan = lastEndTime.Value - DateTimeOffset.UtcNow;
                            if (lastRan < IndexingWaitPeriod)
                            {
                                nextRun = IndexingWaitPeriod + lastRan;
                            }
                        }
                    }

                    switch (statusCode)
                    {
                        case HttpStatusCode.TooManyRequests:
                        {
                            logger.LogError(
                                $"Too Many Requests. Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{ex.Status}'.");
                            return new IndexerStateWrapper(IndexerState.TooManyRequests, nextRun, lastRan);
                        }
                        case HttpStatusCode.Conflict:
                        {
                            logger.LogError(
                                $"Indexer already running. Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{ex.Status}'.");
                            return new IndexerStateWrapper(IndexerState.AlreadyRunning, nextRun, lastRan);
                        }
                        default:
                            throw new InvalidOperationException($"Indeterminate status-code: '{statusCode}'");
                    }

                    break;
                }
                default:
                {
                    logger.LogError(ex,
                        $"Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{ex.Status}' and message '{ex.Message}'.");
                    return new IndexerStateWrapper(IndexerState.Failure);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to run indexer '{_searchIndexConfig.IndexerName}' with message '{ex.Message}'.");
            return new IndexerStateWrapper(IndexerState.Failure);
        }
    }
}