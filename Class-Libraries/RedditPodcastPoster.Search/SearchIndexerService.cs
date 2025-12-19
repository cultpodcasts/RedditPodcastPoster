using System.Net;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
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
        logger.LogInformation("Indexing '{IndexerName}'.", _searchIndexConfig.IndexerName);
        try
        {
            var response = await searchIndexerClient.RunIndexerAsync(_searchIndexConfig.IndexerName);
            response = await searchIndexerClient.RunIndexerAsync(_searchIndexConfig.IndexerName);
            if (response.Status != (int) HttpStatusCode.Accepted)
            {
                logger.LogError(
                    "Failure to run indexer '{IndexerName}' with status '{ResponseStatus}' and reason '{ResponseReasonPhrase}'.", _searchIndexConfig.IndexerName, response.Status, response.ReasonPhrase);
                return new IndexerStateWrapper(IndexerState.Failure);
            }

            logger.LogInformation("Ran indexer '{IndexerName}'.", _searchIndexConfig.IndexerName);
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
                        else if (indexerStatus.Value.LastResult.Status == IndexerExecutionStatus.InProgress)
                        {
                            nextRun = IndexingWaitPeriod;
                            lastRan = DateTimeOffset.UtcNow - indexerStatus.Value.LastResult.StartTime;
                        }
                    }

                    switch (statusCode)
                    {
                        case HttpStatusCode.TooManyRequests:
                        {
                            logger.LogError(
                                "Too Many Requests. Failure to run indexer '{IndexerName}' with status '{ExStatus}'.", _searchIndexConfig.IndexerName, ex.Status);
                            return new IndexerStateWrapper(IndexerState.TooManyRequests, nextRun, lastRan);
                        }
                        case HttpStatusCode.Conflict:
                        {
                            logger.LogError(
                                "Indexer already running. Failure to run indexer '{IndexerName}' with status '{ExStatus}'.", _searchIndexConfig.IndexerName, ex.Status);
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
                        "Failure to run indexer '{IndexerName}' with status '{ExStatus}' and message '{ExMessage}'.", _searchIndexConfig.IndexerName, ex.Status, ex.Message);
                    return new IndexerStateWrapper(IndexerState.Failure);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to run indexer '{IndexerName}' with message '{ExMessage}'.", _searchIndexConfig.IndexerName, ex.Message);
            return new IndexerStateWrapper(IndexerState.Failure);
        }
    }
}