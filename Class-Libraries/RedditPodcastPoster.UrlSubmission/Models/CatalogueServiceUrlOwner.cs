namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// A film, TV episode, or news report that already stores a YouTube, Spotify, or Apple service URL.
/// </summary>
public sealed record CatalogueServiceUrlOwner(string ContentKind, string? ParentName);
