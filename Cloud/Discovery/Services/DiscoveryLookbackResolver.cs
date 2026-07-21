using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Discovery.Models;

namespace Discovery.Services;

public class DiscoveryLookbackResolver(
    IOptions<DiscoverOptions> discoverOptions,
    IDiscoveryResultsRepository discoveryResultsRepository,
    ILogger<DiscoveryLookbackResolver> logger) : IDiscoveryLookbackResolver
{
    private readonly DiscoverOptions _discoverOptions =
        discoverOptions.Value ?? throw new ArgumentException($"Missing {nameof(DiscoverOptions)}.");

    public async Task<DiscoveryLookbackResolution> ResolveAsync(CancellationToken cancellationToken = default)
    {
        DateTime? latestSuccessful;
        try
        {
            latestSuccessful = await GetLatestSuccessfulDiscoveryBeganAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to query latest DiscoveryBegan from Cosmos; Dynamic lookback fail-closed.");
            throw new DiscoveryLookbackUnavailableException(
                "Discovery lookback fail-closed: could not read the latest successful discoveryBegan watermark from Cosmos. " +
                "Fix Cosmos access, then seed the first success with Console-Apps/Discover (see docs/discovery-uk-schedule.md).",
                ex);
        }

        if (latestSuccessful is null)
        {
            logger.LogError(
                "No prior Discovery success watermark in Cosmos; Dynamic lookback fail-closed. First run must be CLI.");
            throw new DiscoveryLookbackUnavailableException(
                "Discovery lookback fail-closed: no prior discoveryBegan watermark in Cosmos. " +
                "The first successful Discovery run MUST be via Console-Apps/Discover (CLI). " +
                "After that, the timer uses Dynamic lookback from the watermark. " +
                "See docs/discovery-uk-schedule.md.");
        }

        var overlap = _discoverOptions.DynamicLookbackOverlap ?? DiscoveryLookbackCalculator.DefaultDynamicOverlap;
        var since = DiscoveryLookbackCalculator.ResolveSince(latestSuccessful.Value, overlap);

        logger.LogInformation(
            "Dynamic lookback: latest Cosmos DiscoveryBegan '{Latest:O}', overlap '{Overlap}', since '{Since:O}'.",
            latestSuccessful, overlap, since);

        return new DiscoveryLookbackResolution(since, latestSuccessful.Value);
    }

    private async Task<DateTime?> GetLatestSuccessfulDiscoveryBeganAsync(CancellationToken cancellationToken)
    {
        var latest = await discoveryResultsRepository.GetLatestDiscoveryBegan(cancellationToken);
        return latest?.ToUniversalTime();
    }
}
