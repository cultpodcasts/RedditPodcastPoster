using CommandLine;

namespace DeleteSearchDocument;

public class DeleteSearchDocumentRequest
{
    [Value(0, Required = true, HelpText = "Guid of the document in the search-index to remove")]
    public Guid DocumentId { get; set; }
}