using Azure.Search.Documents.Indexes;

namespace CreateSearchIndex;

public interface ISearchIndexClientFactory
{
    SearchIndexClient Create();
}