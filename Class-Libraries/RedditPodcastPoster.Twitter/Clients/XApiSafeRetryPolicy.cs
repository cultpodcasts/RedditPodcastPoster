using Polly;
using Polly.Extensions.Http;

namespace RedditPodcastPoster.Twitter.Clients;

/// <summary>
/// Polly retry for the typed X API <see cref="HttpClient"/>. Only idempotent calls (GET timeline, DELETE post)
/// are retried on transient failures / 5xx. Post creation (<c>POST /2/tweets</c>) is never auto-retried: X may
/// have accepted the post, and a second attempt would double-post or hit duplicate-content.
/// </summary>
public static class XApiSafeRetryPolicy
{
    /// <summary>Retries after the first failure (total attempts = <see cref="MaxRetries"/> + 1).</summary>
    public const int MaxRetries = 2;

    /// <summary>
    /// GET and DELETE may be repeated safely. POST (and other methods) must not be auto-retried by this client.
    /// </summary>
    public static bool IsSafeToRetry(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Delete;

    /// <summary>
    /// Per-request policy used by <c>IHttpClientBuilder.AddPolicyHandler</c>. Returns a no-op for post creation.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> ForRequest(HttpRequestMessage request)
    {
        if (!IsSafeToRetry(request.Method))
        {
            return Policy.NoOpAsync<HttpResponseMessage>();
        }

        // HandleTransientHttpError: network failures, 408, and 5xx.
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                MaxRetries,
                attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)));
    }
}
