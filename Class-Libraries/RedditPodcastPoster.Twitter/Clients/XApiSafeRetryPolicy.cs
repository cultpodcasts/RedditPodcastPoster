using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;

namespace RedditPodcastPoster.Twitter.Clients;

/// <summary>
/// Resilience retry for the typed X API <see cref="HttpClient"/>. Only idempotent calls (GET timeline, DELETE post)
/// are retried on transient failures / 5xx. Post creation (<c>POST /2/tweets</c>) is never auto-retried: X may
/// have accepted the post, and a second attempt would double-post or hit duplicate-content.
/// </summary>
public static class XApiSafeRetryPolicy
{
    /// <summary>Name passed to <c>AddResilienceHandler</c>.</summary>
    public const string PipelineName = "x-api-safe-retry";

    /// <summary>Retries after the first failure (total attempts = <see cref="MaxRetries"/> + 1).</summary>
    public const int MaxRetries = 2;

    /// <summary>
    /// Base delay for exponential backoff (seconds-scale, with jitter). Attempt delays grow from this value.
    /// </summary>
    public static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// GET and DELETE may be repeated safely. POST (and other methods) must not be auto-retried by this client.
    /// </summary>
    public static bool IsSafeToRetry(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Delete;

    /// <summary>
    /// Configures a custom resilience pipeline (retry only) for the X API HttpClient.
    /// Do not use <c>AddStandardResilienceHandler</c> — its default retry would retry POST create.
    /// </summary>
    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder) =>
        builder.AddRetry(CreateRetryOptions());

    /// <summary>
    /// Retry options used by <see cref="Configure"/> and by BusinessRules that exercise the pipeline.
    /// Default <see cref="HttpRetryStrategyOptions.ShouldHandle"/> covers network failures, 408, and 5xx;
    /// method safety is gated via <see cref="IsSafeToRetry"/> (DELETE stays eligible — unlike
    /// <c>DisableForUnsafeHttpMethods</c>).
    /// </summary>
    public static HttpRetryStrategyOptions CreateRetryOptions()
    {
        var options = new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = MaxRetries,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = RetryBaseDelay
        };

        var defaultShouldHandle = options.ShouldHandle;
        options.ShouldHandle = async args =>
        {
            var request = args.Outcome.Result?.RequestMessage ?? args.Context.GetRequestMessage();
            if (request is null || !IsSafeToRetry(request.Method))
            {
                return false;
            }

            return await defaultShouldHandle(args).ConfigureAwait(false);
        };

        return options;
    }
}
