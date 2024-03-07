using CommandLine;

namespace DeleteSearchDocument;

public class DeleteSearchDocumentRequest
{
    [Value(0, Required = true,
        HelpText = "Guid of the document in the search-index to remove, or podcast-id when using podcast flag")]
    public Guid DocumentId { get; set; }

    [Option('p', "podcast", Default = false, Required = false,
        HelpText = "To delete all podcast episodes from the search-index use this flag and provide the podcast id")]
    public bool IsPodcast { get; set; }
}