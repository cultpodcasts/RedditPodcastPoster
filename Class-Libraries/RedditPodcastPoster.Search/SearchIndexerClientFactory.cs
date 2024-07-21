using Azure;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.Search;

public class SearchIndexerClientFactory(
    IOptions<SearchIndexConfig> searchIndexConfig,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SearchIndexerClientFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISearchIndexerClientFactory
{
    private readonly SearchIndexConfig _searchIndexConfig = searchIndexConfig.Value;

    public SearchIndexerClient Create()
    {
        var credential = new AzureKeyCredential(_searchIndexConfig.Key);
        var client = new SearchIndexerClient(_searchIndexConfig.Url, credential);

        return client;
    }
}