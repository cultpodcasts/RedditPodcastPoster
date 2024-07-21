using System.Net;
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
    private readonly SearchIndexConfig _searchIndexConfig = searchIndexConfig.Value;

    public async Task RunIndexer()
    {
        logger.LogInformation($"Indexing '{_searchIndexConfig.IndexerName}'.");
        var response = await searchIndexerClient.RunIndexerAsync(_searchIndexConfig.IndexerName);
        if (response.Status != (int) HttpStatusCode.Accepted)
        {
            logger.LogError(
                $"Failure to run indexer '{_searchIndexConfig.IndexerName}' with status '{response.Status}' and reason '{response.ReasonPhrase}'.");
        }
    }
}