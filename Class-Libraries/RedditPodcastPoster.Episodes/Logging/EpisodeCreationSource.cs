namespace RedditPodcastPoster.Episodes.Logging;

/// <summary>
/// Provenance of an episode create: which product path persisted the new episode.
/// </summary>
public enum EpisodeCreationSource
{
    Indexer = 1,
    SubmitUrl,
    Discovery
}
