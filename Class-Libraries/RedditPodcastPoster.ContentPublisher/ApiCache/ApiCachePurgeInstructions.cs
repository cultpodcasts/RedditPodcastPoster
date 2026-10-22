namespace RedditPodcastPoster.ContentPublisher.ApiCache;

/// <param name="Urls">Every Api URL to purge.</param>
/// <param name="Commands">One Cloudflare CLI command per batch of URLs (PowerShell, via the <c>cf.cmd</c> shim).</param>
/// <param name="VerifyNote">One line explaining how to confirm the purge worked.</param>
public record ApiCachePurgeInstructions(
    IReadOnlyList<string> Urls,
    IReadOnlyList<string> Commands,
    string VerifyNote);
