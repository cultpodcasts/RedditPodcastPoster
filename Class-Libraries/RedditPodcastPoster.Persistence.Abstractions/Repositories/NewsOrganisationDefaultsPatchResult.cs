namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>Outcome of <see cref="INewsOrganisationRepository.PatchCreationDefaults"/>.</summary>
public enum NewsOrganisationDefaultsPatchResult
{
    /// <summary>Both flags are now stored as true.</summary>
    Patched,

    /// <summary>The news organisation no longer exists.</summary>
    NotFound,

    /// <summary>The document's <c>indexing</c> object was added or removed since it was read; nothing was written.</summary>
    ShapeChanged
}
