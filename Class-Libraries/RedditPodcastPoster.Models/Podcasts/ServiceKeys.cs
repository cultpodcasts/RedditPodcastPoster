namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Canonical JSON keys for Spotify, Apple, and YouTube on <c>episode.services</c> / <c>ids</c>.
/// Streaming destinations use <see cref="StreamingService"/> / <see cref="StreamingServiceWire"/>.
/// </summary>
public static class ServiceKeys
{
    public const string Spotify = "spotify";
    public const string Apple = "apple";
    public const string YouTube = "youtube";
}
