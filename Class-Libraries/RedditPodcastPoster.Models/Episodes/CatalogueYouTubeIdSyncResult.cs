namespace RedditPodcastPoster.Models.Episodes;

/// <summary>What <see cref="EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl"/> did to the compact id.</summary>
public enum CatalogueYouTubeIdSyncOutcome
{
    /// <summary>Id already matched the url (or there is neither a url nor an id).</summary>
    Unchanged,

    /// <summary>No id before; set from the url.</summary>
    Filled,

    /// <summary>A different id before; replaced by the url's id (url wins).</summary>
    Overwritten,

    /// <summary>Url missing or not a video url; any existing id was kept, never cleared.</summary>
    SkippedUnparseable
}

public readonly record struct CatalogueYouTubeIdSyncResult(
    CatalogueYouTubeIdSyncOutcome Outcome,
    string? Before,
    string? After)
{
    public bool Changed => Outcome is CatalogueYouTubeIdSyncOutcome.Filled or CatalogueYouTubeIdSyncOutcome.Overwritten;
}
