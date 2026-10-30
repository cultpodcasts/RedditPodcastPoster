using System.Net;
using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions;

/// <summary>
/// Shared limits for bulk point writes that land on one logical partition (a podcast's episodes, an organisation's
/// reports, a show's episodes). The account is serverless, so request units bind long before connections do: keep
/// parallelism modest and retry throttling on purpose instead of failing part way.
/// </summary>
public static class CatalogueBulkWrite
{
    /// <summary>Concurrent point writes or deletes against one logical partition.</summary>
    public const int Parallelism = 8;

    /// <summary>Retries after the Cosmos SDK's own throttle retries are used up.</summary>
    public const int MaxThrottleRetries = 5;

    /// <summary>First backoff when a 429 carries no Retry-After; doubles per retry.</summary>
    public static readonly TimeSpan ThrottleBaseDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>Longest single wait between throttle retries.</summary>
    public static readonly TimeSpan ThrottleMaxDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs <paramref name="operation"/>, retrying a 429 <see cref="CosmosException"/> up to
    /// <see cref="MaxThrottleRetries"/> times after <see cref="CosmosException.RetryAfter"/> (or an exponential
    /// backoff). <paramref name="onThrottled"/> runs once per retry, so callers can count them.
    /// </summary>
    public static async Task<T> RetryThrottled<T>(Func<Task<T>> operation, Action? onThrottled = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests &&
                                             attempt < MaxThrottleRetries)
            {
                onThrottled?.Invoke();
                await Task.Delay(ThrottleDelay(ex.RetryAfter, attempt));
            }
        }
    }

    /// <inheritdoc cref="RetryThrottled{T}"/>
    public static Task RetryThrottled(Func<Task> operation, Action? onThrottled = null) =>
        RetryThrottled(async () =>
        {
            await operation();
            return true;
        }, onThrottled);

    public static TimeSpan ThrottleDelay(TimeSpan? retryAfter, int attempt)
    {
        var delay = retryAfter is { } after && after > TimeSpan.Zero
            ? after
            : ThrottleBaseDelay * Math.Pow(2, attempt);
        return delay > ThrottleMaxDelay ? ThrottleMaxDelay : delay;
    }
}
