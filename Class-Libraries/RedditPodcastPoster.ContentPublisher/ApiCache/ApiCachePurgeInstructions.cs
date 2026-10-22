namespace RedditPodcastPoster.ContentPublisher.ApiCache;

/// <param name="Urls">Every Api URL to purge.</param>
/// <param name="Commands">One Cloudflare CLI command per batch of URLs (PowerShell, via the <c>cf.cmd</c> shim).</param>
/// <param name="VerifyLines">One verify line per purged route, then the browser-cache caveat.</param>
public record ApiCachePurgeInstructions(
    IReadOnlyList<string> Urls,
    IReadOnlyList<string> Commands,
    IReadOnlyList<string> VerifyLines);
