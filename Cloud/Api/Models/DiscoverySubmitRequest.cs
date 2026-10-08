namespace Api.Models;

public class DiscoverySubmitRequest
{
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    public Guid[] ResultIds { get; set; } = [];
}
