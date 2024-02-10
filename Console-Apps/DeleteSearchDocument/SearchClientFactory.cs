using Azure;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeleteSearchDocument;

public class SearchClientFactory(
    IOptions<SearchIndexConfig> searchIndexConfig,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SearchClientFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISearchClientFactory
{
    private readonly SearchIndexConfig _searchIndexConfig = searchIndexConfig.Value;

    public SearchClient Create()
    {
        var credential = new AzureKeyCredential(_searchIndexConfig.Key);
        var client = new SearchClient(_searchIndexConfig.Url, _searchIndexConfig.IndexName, credential);

        return client;
    }
}