using Microsoft.Extensions.Logging;

using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <inheritdoc />
public sealed class CatalogueRequestGuard(
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<EpisodeLookupOptions> options,
    ILogger<CatalogueRequestGuard> logger)
    : ICatalogueRequestGuard
{
    /// <summary>
    /// <see cref="EpisodeLookupOptions.RequestTimeout"/> (default 10 s), the same for every flow.
    /// Apple previously had no guard (HttpClient's 100 s default).
    /// </summary>
    public TimeSpan Timeout { get; } = options.Value.RequestTimeout;

    public const string GuardFiredMessagePrefix = "Catalogue request timeout guard fired:";

    public async Task<T?> Run<T>(
        string platform,
        string operation,
        Func<CancellationToken, Task<T?>> request,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(Timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await request(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                GuardFiredMessagePrefix +
                " platform='{Platform}' operation='{Operation}' timeout-seconds={TimeoutSeconds}.",
                platform, operation, Timeout.TotalSeconds);
            return default;
        }
    }
}
