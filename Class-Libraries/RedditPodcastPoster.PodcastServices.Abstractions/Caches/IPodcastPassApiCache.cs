namespace RedditPodcastPoster.PodcastServices.Abstractions.Caches;

/// <summary>
/// Pass-scoped API response cache bag. Cleared between podcasts within a batch Indexer scope.
/// </summary>
public interface IPodcastPassApiCache
{
    void Clear();
}
