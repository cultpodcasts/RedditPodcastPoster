using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// Eagerly loads Cosmos title-casing rules and precompiles English/universal regex maps
/// so the first homepage/episode sanitise does not pay cold-start compile cost.
/// </summary>
public sealed class TitleCasingRulesWarmupHostedService(
    IAsyncInstance<ITitleCasingRulesProvider> titleCasingRulesProvider,
    ILogger<TitleCasingRulesWarmupHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await titleCasingRulesProvider.GetAsync();
        logger.LogInformation("Warmed {Provider}.", nameof(ITitleCasingRulesProvider));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
