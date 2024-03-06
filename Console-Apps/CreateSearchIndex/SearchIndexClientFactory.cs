using Azure;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreateSearchIndex;

public class SearchIndexClientFactory(
    IOptions<SearchIndexConfig> searchIndexConfig,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SearchIndexClientFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISearchIndexClientFactory
{
    private readonly SearchIndexConfig _searchIndexConfig = searchIndexConfig.Value;

    public SearchIndexClient Create()
    {
        var credential = new AzureKeyCredential(_searchIndexConfig.Key);
        var client = new SearchIndexClient(_searchIndexConfig.Url, credential);

        return client;
    }
}