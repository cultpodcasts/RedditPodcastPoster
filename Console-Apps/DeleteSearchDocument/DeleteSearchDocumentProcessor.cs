using Azure.Search.Documents;
using Microsoft.Extensions.Logging;

namespace DeleteSearchDocument;

public class DeleteSearchDocumentProcessor
{
    private readonly ILogger<DeleteSearchDocumentProcessor> _logger;

    private readonly SearchClient _searchClient;

    public DeleteSearchDocumentProcessor(
        SearchClient searchClient,
        ILogger<DeleteSearchDocumentProcessor> logger)
    {
        _searchClient = searchClient;
        _logger = logger;
    }

    public async Task Process(DeleteSearchDocumentRequest request)
    {
        var result = await _searchClient.DeleteDocumentsAsync(
            "id",
            new[] {request.DocumentId.ToString()},
            new IndexDocumentsOptions {ThrowOnAnyError = true});
        var success = result.Value.Results.First().Succeeded;
        if (!success)
        {
            _logger.LogError(result.Value.Results.First().ErrorMessage);
        }
    }
}