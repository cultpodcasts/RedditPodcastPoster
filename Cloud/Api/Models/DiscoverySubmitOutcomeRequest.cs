namespace Api.Models;

/// <summary>Query model for POST DiscoveryCuration/submit-outcome (document + result ids).</summary>
public class DiscoverySubmitOutcomeRequest
{
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    public Guid[] ResultIds { get; set; } = [];
}
