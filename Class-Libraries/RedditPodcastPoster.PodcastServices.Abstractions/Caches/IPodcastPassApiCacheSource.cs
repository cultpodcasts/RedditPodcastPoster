namespace RedditPodcastPoster.PodcastServices.Abstractions.Caches;

/// <summary>
/// Internal clearable for services that hold pass-scoped API response memory.
/// Not part of platform domain APIs (channel/episode/playlist fetch).
/// </summary>
public interface IPodcastPassApiCacheSource
{
    void ClearPassCache();
}
