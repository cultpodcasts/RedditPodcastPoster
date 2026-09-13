namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Canonical JSON keys for Spotify, Apple, and YouTube on <c>episode.services</c> / <c>ids</c>.
/// Streaming keys live on <c>StreamingServiceKeys</c> next to provider registrations.
/// </summary>
public static class ServiceKeys
{
    public const string Spotify = "spotify";
    public const string Apple = "apple";
    public const string YouTube = "youtube";
}
