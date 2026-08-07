using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.DependencyInjection;

/// <summary>
/// Runs all registered <see cref="IStartupWarmer"/> instances concurrently at host start.
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

        await Task.WhenAll(list.Select(warmer => WarmOneAsync(warmer, cancellationToken)));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task WarmOneAsync(IStartupWarmer warmer, CancellationToken cancellationToken)
    {
        await warmer.WarmAsync(cancellationToken);
        logger.LogInformation("Warmed {Warmer}.", warmer.Name);
    }
}
