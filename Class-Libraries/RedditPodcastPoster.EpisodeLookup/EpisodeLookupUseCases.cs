using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// Public entry points, one per use case. A caller marks its <see cref="IndexingContext"/> with the use case before
/// calling the Spotify or Apple resolvers; the planner then applies that use case's chain from
/// <see cref="EpisodeLookupChains"/>. The flag is set in place (not copied with <c>with</c>) so circuit-breaker flags on
/// the shared context stay shared. To add a use case, see README.md in this project.
/// </summary>
public static class EpisodeLookupUseCases
{
    /// <summary>
    /// Indexer (<c>PodcastUpdater</c>: Cloud indexer, Api index, Index CLI). Newest pages only, never a title search;
    /// a platform with no show id on the podcast is skipped.
    /// </summary>
    public static IndexingContext ForIndex(IndexingContext context) => Mark(context, EpisodeLookupFlow.Index);

    /// <summary>URL submission and the Spotify/Apple URL categorisers (also the default for an unmarked context).</summary>
    public static IndexingContext ForSubmit(IndexingContext context) => Mark(context, EpisodeLookupFlow.Submit);

    /// <summary>Discovery Spotify/Apple enrichers.</summary>
    public static IndexingContext ForDiscovery(IndexingContext context) => Mark(context, EpisodeLookupFlow.Discovery);

    /// <summary><c>EnrichExistingEpisodesFromPodcastServices</c> CLI.</summary>
    public static IndexingContext ForEnrich(IndexingContext context) => Mark(context, EpisodeLookupFlow.Enrich);

    private static IndexingContext Mark(IndexingContext context, EpisodeLookupFlow flow)
    {
        context.LookupFlow = flow;
        return context;
    }
}
