using Azure.Search.Documents;
using Microsoft.Extensions.Logging;

namespace DeleteSearchDocument;

public class DeleteSearchDocumentProcessor(
    SearchClient searchClient,
    ILogger<DeleteSearchDocumentProcessor> logger)
{
    public async Task Process(DeleteSearchDocumentRequest request)
    {
        var result = await searchClient.DeleteDocumentsAsync(
            "id",
            new[] {request.DocumentId.ToString()},
            new IndexDocumentsOptions {ThrowOnAnyError = true});
        var success = result.Value.Results.First().Succeeded;
        if (!success)
        {
            logger.LogError(result.Value.Results.First().ErrorMessage);
        }
    }
}