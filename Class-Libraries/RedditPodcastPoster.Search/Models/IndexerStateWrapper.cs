namespace RedditPodcastPoster.Search.Models;

public record IndexerStateWrapper(
    IndexerState IndexerState,
    TimeSpan? NextRun = null,
    TimeSpan? LastRan = null);