using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Options for catalogue lookup by title and duration (Spotify/Apple resolver paths).
/// </summary>
public sealed record CatalogueMatchByLengthOptions(
    Service? ReleaseAuthority = null,
    bool AcceptUniqueDurationWithoutTitleMatch = false,
    bool EnrichingYouTubeDiscoveredEpisode = false);
