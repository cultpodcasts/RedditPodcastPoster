using Azure;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeleteSearchDocument;

public class SearchClientFactory(
    IOptions<SearchIndexConfig> searchIndexConfig,
    ILogger<SearchClientFactory> logger)
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