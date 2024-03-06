using Azure.Search.Documents.Indexes;

namespace CreateSearchIndex;

public interface ISearchIndexerClientFactory
{
    SearchIndexerClient Create();
}