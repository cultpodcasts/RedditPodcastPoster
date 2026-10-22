namespace RedditPodcastPoster.ContentPublisher.ApiCache;

/// <summary>
/// Builds the Cloudflare CLI command(s) an operator runs to purge the Api Worker cache after an R2 publish.
/// Nothing here calls Cloudflare: no token or secret is involved.
/// </summary>
public interface IApiCachePurgeCommandBuilder
{
    /// <summary>Returns <c>null</c> when none of the published content is cached by the Api.</summary>
    ApiCachePurgeInstructions? Build(IEnumerable<ApiCachedContent> published);

    /// <summary>A console-ready block (banner, command(s), verify note) for <paramref name="instructions"/>.</summary>
    string Format(ApiCachePurgeInstructions instructions);
}
