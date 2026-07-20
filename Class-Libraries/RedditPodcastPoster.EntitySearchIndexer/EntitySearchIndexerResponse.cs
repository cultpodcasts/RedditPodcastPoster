using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer;

public record EntitySearchIndexerResponse
{
    public IndexerState? IndexerState { get; init; }

    public EpisodeIndexRequestState? EpisodeIndexRequestState { get; init; }
}