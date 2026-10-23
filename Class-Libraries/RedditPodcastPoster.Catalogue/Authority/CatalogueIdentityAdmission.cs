namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// How the caller answered an ambiguous-identity prompt. Unset means they have not answered.
/// </summary>
public enum CatalogueIdentityChoice
{
    Unset = 0,
    Select,
    Continue,
    Cancel
}

public static class CatalogueIdentityChoices
{
    public static CatalogueIdentityChoice Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "select" => CatalogueIdentityChoice.Select,
            "continue" => CatalogueIdentityChoice.Continue,
            "cancel" => CatalogueIdentityChoice.Cancel,
            _ => CatalogueIdentityChoice.Unset
        };
}

/// <summary>
/// Ambiguous authority matches that must be answered before create may finish.
/// </summary>
public sealed record CatalogueIdentityHold(
    string Target,
    string Query,
    IReadOnlyList<CatalogueAuthorityCandidate> Candidates,
    Guid? DiscoveryItemId = null);

/// <summary>
/// Outcome of the pre-create identity check. <see cref="Hold"/> means nothing may be written yet.
/// </summary>
public sealed record CatalogueIdentityAdmission(
    bool Hold,
    CatalogueIdentityHold? Prompt,
    CatalogueAuthorityIds? Ids,
    /// <summary>When resolving a TV episode, ids collected for the parent show (fill blanks only).</summary>
    CatalogueAuthorityIds? ParentIds = null,
    /// <summary>Which document the <see cref="Prompt"/> candidates apply to.</summary>
    string? HoldTarget = null)
{
    public static CatalogueIdentityAdmission Proceed(
        CatalogueAuthorityIds? ids = null,
        CatalogueAuthorityIds? parentIds = null) =>
        new(false, null, ids, parentIds);

    public static CatalogueIdentityAdmission NeedsPrompt(CatalogueIdentityHold prompt) =>
        new(true, prompt, null, null, prompt.Target);
}
