using Azure;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeleteSearchDocument;

public class SearchClientFactory : ISearchClientFactory
{
    private readonly ILogger<SearchClientFactory> _logger;
    private readonly SearchIndexConfig _searchIndexConfig;

    public SearchClientFactory(
        IOptions<SearchIndexConfig> searchIndexConfig,
        ILogger<SearchClientFactory> logger)
    {
        _searchIndexConfig = searchIndexConfig.Value;
        _logger = logger;
    }

    public SearchClient Create()
    {
        var credential = new AzureKeyCredential(_searchIndexConfig.Key);
        var client = new SearchClient(_searchIndexConfig.Url, _searchIndexConfig.IndexName, credential);

        return client;
    }
}