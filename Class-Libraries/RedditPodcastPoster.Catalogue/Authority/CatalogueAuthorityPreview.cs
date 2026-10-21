namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// Dry-run view of a title search. Ids are set only for <see cref="CatalogueAuthorityResolution.Unique"/>.
/// Several programmes, a different title, or no hit are finished decisions: they are not one show.
/// </summary>
public sealed record CatalogueAuthorityPreview(
    string Query,
    int HitCount,
    string? ImdbId,
    int? TmdbId,
    long? TvdbId,
    IReadOnlyList<string> HitLabels,
    bool LookupFailed = false,
    CatalogueAuthorityResolution Resolution = CatalogueAuthorityResolution.NotInAuthority);
