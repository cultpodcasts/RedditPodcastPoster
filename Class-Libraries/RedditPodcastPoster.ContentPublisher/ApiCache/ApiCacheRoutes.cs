namespace RedditPodcastPoster.ContentPublisher.ApiCache;

/// <summary>
/// The single list of public Api URLs that cache R2 content. The Worker cache key is the full request URL,
/// so these must match exactly what the website requests (no query string:
/// <c>new URL('/search-suggestions', environment.api)</c> and <c>new URL('/homepage', environment.api)</c>).
/// Both hostnames read the same <c>content</c> R2 bucket (see Api wrangler.jsonc), so both are purged.
/// </summary>
public static class ApiCacheRoutes
{
    /// <summary>Cloudflare zone holding both Api hostnames.</summary>
    public const string Zone = "cultpodcasts.com";

    /// <summary>Production then preview (staging) Api origins.</summary>
    public static readonly IReadOnlyList<string> ApiOrigins =
    [
        "https://api.cultpodcasts.com",
        "https://api-preview.cultpodcasts.com"
    ];

    public static string PathFor(ApiCachedContent content) => content switch
    {
        ApiCachedContent.Homepage => "/homepage",
        ApiCachedContent.SearchSuggestions => "/search-suggestions",
        _ => throw new ArgumentOutOfRangeException(nameof(content), content, null)
    };

    /// <summary>Response header the Api sets to <c>HIT</c>/<c>MISS</c> for this route.</summary>
    public static string CacheHeaderFor(ApiCachedContent content) => content switch
    {
        ApiCachedContent.Homepage => "X-Homepage-Cache",
        ApiCachedContent.SearchSuggestions => "X-Search-Suggestions-Cache",
        _ => throw new ArgumentOutOfRangeException(nameof(content), content, null)
    };

    /// <summary>Distinct URLs for the given content, in enum order then origin order.</summary>
    public static IReadOnlyList<string> UrlsFor(IEnumerable<ApiCachedContent> contents) =>
        contents
            .Distinct()
            .Order()
            .SelectMany(content => ApiOrigins.Select(origin => origin + PathFor(content)))
            .ToList();
}
