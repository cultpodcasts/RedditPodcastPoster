using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.DependencyInjection;

/// <summary>
/// Runs all registered <see cref="IStartupWarmer"/> instances concurrently at host start.
/// Failures are logged per warmer (and summarised) then rethrown so host start fails loud.
/// </summary>
public sealed class ParallelStartupWarmupHostedService(
    IEnumerable<IStartupWarmer> warmers,
    ILogger<ParallelStartupWarmupHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var list = warmers as IStartupWarmer[] ?? warmers.ToArray();
        if (list.Length == 0)
        {
            return;
        }

        var outcomes = await Task.WhenAll(list.Select(warmer => WarmOneAsync(warmer, cancellationToken)));
        var failures = outcomes.Where(o => o.Error is not null).ToArray();
        if (failures.Length == 0)
        {
            return;
        }

        var failedNames = string.Join(", ", failures.Select(f => f.Name));
        logger.LogError(
            "StartupWarmFailed count='{FailureCount}' warmers='{FailedWarmers}'.",
            failures.Length,
            failedNames);

        throw new AggregateException(
            $"Startup warm failed for {failures.Length} warmer(s): {failedNames}.",
            failures.Select(f => f.Error!));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<WarmOutcome> WarmOneAsync(IStartupWarmer warmer, CancellationToken cancellationToken)
    {
        try
        {
            await warmer.WarmAsync(cancellationToken);
            logger.LogInformation("Warmed {Warmer}.", warmer.Name);
            return new WarmOutcome(warmer.Name, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Stable App Insights search: Message contains "StartupWarmFailed" or "Startup warm failed for".
            logger.LogError(
                ex,
                "Startup warm failed for {Warmer}.",
                warmer.Name);
            return new WarmOutcome(warmer.Name, ex);
        }
    }

    private readonly record struct WarmOutcome(string Name, Exception? Error);
}
