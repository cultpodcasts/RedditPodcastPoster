namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// What a title search decided. Only <see cref="Unique"/> may supply ids.
/// </summary>
public enum CatalogueAuthorityResolution
{
    /// <summary>One hit whose name agrees with the query.</summary>
    Unique,

    /// <summary>More than one programme. A channel or a strand is not one show.</summary>
    SeveralProgrammes,

    /// <summary>One hit whose name does not agree with the query.</summary>
    DifferentTitle,

    /// <summary>No hit on the title that was searched.</summary>
    NotInAuthority
}
