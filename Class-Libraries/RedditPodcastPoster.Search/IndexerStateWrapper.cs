namespace RedditPodcastPoster.Search;

public record IndexerStateWrapper(
    IndexerState IndexerState,
    TimeSpan? NextRun = null,
    TimeSpan? LastRan = null);