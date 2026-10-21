using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Platform-agnostic description of the episode being looked up in an audio catalogue (Spotify, Apple, …).
/// Every platform adapter maps its request to this record so planning, candidate sourcing and matching are identical.
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
    /// <summary>Use-case doing the lookup (from <c>IndexingContext.LookupFlow</c>).</summary>
    public EpisodeLookupFlow Flow { get; init; } = EpisodeLookupFlow.Submit;

    /// <summary>Start of the caller's date window (<c>IndexingContext.ReleasedSince</c>), when it has one.</summary>
    public DateTime? WindowStart { get; init; }

    /// <summary>
    /// Show size stored on the podcast for this platform (<c>spotifyEpisodeCount</c> / <c>appleEpisodeCount</c>).
    /// Null means unknown, which the planner treats as large unless the source can count it cheaply.
    /// </summary>
    public int? ShowEpisodeCount { get; init; }

    /// <summary>
    /// Duration-aware matching applies when the probe has a length and comes from YouTube
    /// (YouTube release authority or YouTube-discovered enrichment), because YouTube and audio releases
    /// lag each other and titles are often edited. Otherwise audio-to-audio lookups match by release date.
    /// </summary>
    public bool UsesLengthStrategy =>
        Length is { } length && length > TimeSpan.Zero &&
        (ReleaseAuthority == Service.YouTube || EnrichingYouTubeDiscoveredEpisode);
}
