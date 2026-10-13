using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Platform-agnostic description of the episode being looked up in an audio catalogue (Spotify, Apple, …).
/// Every platform adapter maps its request to this record so candidate sourcing and matching are identical.
/// </summary>
public sealed record CatalogueLookup(
    string PodcastName,
    string Title,
    DateTime? Released,
    TimeSpan? Length,
    Service? ReleaseAuthority = null,
    bool EnrichingYouTubeDiscoveredEpisode = false,
    string? Description = null,
    string? DefaultSubject = null,
    IReadOnlyList<string>? IgnoredSubjects = null,
    string? Language = null,
    TimeSpan? YouTubePublishingDelay = null)
{
    /// <summary>
    /// Duration-aware matching applies when the probe has a length and comes from YouTube
    /// (YouTube release authority or YouTube-discovered enrichment), because YouTube and audio releases
    /// lag each other and titles are often edited. Otherwise audio-to-audio lookups match by release date.
    /// </summary>
    public bool UsesLengthStrategy =>
        Length is { } length && length > TimeSpan.Zero &&
        (ReleaseAuthority == Service.YouTube || EnrichingYouTubeDiscoveredEpisode);
}
