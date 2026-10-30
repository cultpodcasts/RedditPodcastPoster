namespace Api.Models;

public enum DiscoveryCurationSubmitStatus
{
    Ok,
    Failed,

    /// <summary>A requested report is held for supervisor review (or otherwise not curatable).</summary>
    Conflict,

    /// <summary>At least one item's language is not supported yet; nothing was claimed or created.</summary>
    LanguageDecisionRequired,

    /// <summary>At least one item's authority search is ambiguous; nothing was claimed or created.</summary>
    IdentityDecisionRequired
}

public record DiscoveryCurationSubmitResult(
    DiscoveryCurationSubmitStatus Status,
    DiscoverySubmitOutcome? Outcome = null,
    IReadOnlyList<Guid>? UnavailableDocumentIds = null);
