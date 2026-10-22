namespace RedditPodcastPoster.ContentPublisher.ApiCache;

/// <summary>
/// R2 content whose Api Worker route stores responses in <c>caches.default</c> (keyed by full request URL),
/// so a fresh R2 object is not served until the cached copy expires or is purged.
/// Other R2 keys (languages, people, subjects, flairs, discovery-info, homepage-ssr) are streamed straight
/// from R2 by the Api and are not edge-cached, so they need no purge.
/// </summary>
public enum ApiCachedContent
{
    /// <summary>R2 key <c>homepage</c>; Api <c>GET /homepage</c> (src/homepage.ts, max-age 300).</summary>
    Homepage,

    /// <summary>R2 key <c>search-suggestions</c>; Api <c>GET /search-suggestions</c> (src/searchSuggestions.ts, max-age 3600).</summary>
    SearchSuggestions
}
