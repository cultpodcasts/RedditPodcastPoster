namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// A film, TV episode, or news report that already stores a YouTube, Spotify, or Apple service URL (for a film,
/// possibly as a trailer). <see cref="PlayableId"/> is that item's id.
/// </summary>
public sealed record CatalogueServiceUrlOwner(string ContentKind, string? ParentName, Guid? PlayableId = null);
