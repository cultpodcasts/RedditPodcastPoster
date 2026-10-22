using RedditPodcastPoster.ContentPublisher.ApiCache;

namespace PublishR2;

/// <param name="Success">False when any requested R2 publish failed.</param>
/// <param name="ApiCachePurge">
/// Cloudflare CLI purge instructions for Api-cached content that WAS published in this run
/// (even if a later target failed); <c>null</c> when nothing published is cached by the Api.
/// </param>
public record R2PublishResult(bool Success, ApiCachePurgeInstructions? ApiCachePurge);
