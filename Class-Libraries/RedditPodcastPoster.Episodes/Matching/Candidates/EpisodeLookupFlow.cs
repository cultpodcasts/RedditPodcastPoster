namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Which use-case is looking an episode up. The planner picks a different strategy chain per flow
/// (docs/catalogue-pagination.md "Episode lookup planner"). Callers set it on <c>IndexingContext.LookupFlow</c>;
/// the default is <see cref="Submit"/>, the cheapest chain.
/// </summary>
public enum EpisodeLookupFlow
{
    /// <summary>Submit-url (episode URL and matching other services) and any caller that does not say otherwise.</summary>
    Submit = 0,

    /// <summary>
    /// Indexer passes (hourly Cloud indexer, Api single-podcast index, Index CLI) via <c>PodcastUpdater</c>.
    /// A platform with no show id on the podcast is skipped (no title search).
    /// </summary>
    Index = 1,

    /// <summary>Discovery enrichment of search results.</summary>
    Discovery = 2,

    /// <summary>EnrichExistingEpisodesFromPodcastServices CLI.</summary>
    Enrich = 3
}
