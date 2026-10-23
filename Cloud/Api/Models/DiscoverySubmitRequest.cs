namespace Api.Models;

public class DiscoverySubmitRequest
{
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    public Guid[] ResultIds { get; set; } = [];

    /// <summary>Discovery result id to <c>add</c> or <c>continue</c> after an unsupported-language prompt.</summary>
    public Dictionary<Guid, string>? LanguageDecisions { get; set; }
}
