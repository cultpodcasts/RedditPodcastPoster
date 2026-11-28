namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// A film, TV episode, TV show trailer, or news report that already stores a YouTube, Spotify, or Apple service URL
/// (for a film or a TV show, possibly as a trailer). <see cref="PlayableId"/> is that item's id; <see cref="ParentId"/>
/// is its TV show / news organisation id (null for a film).
/// </summary>
/// <param name="IsTrailer">
/// True when a TV show holds the URL as a trailer (RPP #1412). The owner is then the show: <see cref="ContentKind"/>
/// is TvShowEpisode, <see cref="ParentName"/> / <see cref="ParentId"/> are the show's, and <see cref="PlayableId"/> is
/// null (a trailer is not a TV episode). A trailer has no resubmit path, so it always stops a submit, as a film does
/// (<see cref="AlwaysStopsSubmit"/>). A film trailer is a <see cref="Categorisation.SubmitClassification.Film"/> owner.
/// </param>
public sealed record CatalogueServiceUrlOwner(
    string ContentKind,
    string? ParentName,
    Guid? PlayableId = null,
    Guid? ParentId = null,
    bool IsTrailer = false)
{
    /// <summary>
    /// True for a film (or film trailer) and a TV show trailer: neither has a podcast-platform resubmit path, so a
    /// submit of an owned URL always stops as already-exists instead of being filed under a parent.
    /// </summary>
    public bool AlwaysStopsSubmit => IsTrailer || ContentKind == Categorisation.SubmitClassification.Film;
}
