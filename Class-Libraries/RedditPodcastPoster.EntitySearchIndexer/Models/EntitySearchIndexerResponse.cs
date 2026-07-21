using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Models;

public record EntitySearchIndexerResponse
{
    public IndexerState? IndexerState { get; init; }

    public EpisodeIndexRequestState? EpisodeIndexRequestState { get; init; }
}
