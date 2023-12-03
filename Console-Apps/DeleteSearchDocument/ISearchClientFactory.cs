using Azure.Search.Documents;

namespace DeleteSearchDocument;

public interface ISearchClientFactory
{
    SearchClient Create();
}