using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Discovery;

public class DiscoveryLookbackResolver(
    IOptions<DiscoverOptions> discoverOptions,
    IDiscoveryResultsRepository discoveryResultsRepository,
    IDiscoveryInfoRepository discoveryInfoRepository,
    ILogger<DiscoveryLookbackResolver> logger) : IDiscoveryLookbackResolver
{
    private readonly DiscoverOptions _discoverOptions =
        discoverOptions.Value ?? throw new ArgumentException($"Missing {nameof(DiscoverOptions)}.");

    public async Task<DiscoveryLookbackResolution> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var searchSince = TimeSpan.Parse(_discoverOptions.SearchSince);
        var utcNow = DateTime.UtcNow;
        var mode = _discoverOptions.LookbackMode
            ?? throw new InvalidOperationException(
                $"{nameof(DiscoverOptions)}.{nameof(DiscoverOptions.LookbackMode)} is required.");

        if (mode == DiscoveryLookbackMode.Static)
        {
            var staticSince = DiscoveryLookbackCalculator.ResolveSince(
                utcNow, searchSince, DiscoveryLookbackMode.Static, null);
            return new DiscoveryLookbackResolution(staticSince, DiscoveryLookbackMode.Static, null);
        }

        var latestSuccessful = await GetLatestSuccessfulDiscoveryBeganAsync(cancellationToken);
        var overlap = _discoverOptions.DynamicLookbackOverlap ?? DiscoveryLookbackCalculator.DefaultDynamicOverlap;
        var since = DiscoveryLookbackCalculator.ResolveSince(
            utcNow, searchSince, DiscoveryLookbackMode.Dynamic, latestSuccessful, overlap);

        if (latestSuccessful is null)
        {
            logger.LogInformation(
                "Dynamic lookback: no prior Discovery run signal; falling back to static SearchSince '{SearchSince}'.",
                _discoverOptions.SearchSince);
            return new DiscoveryLookbackResolution(since, DiscoveryLookbackMode.Static, null);
        }

        logger.LogInformation(
            "Dynamic lookback: latest successful DiscoveryBegan '{Latest:O}', overlap '{Overlap}', since '{Since:O}'.",
            latestSuccessful, overlap, since);

        return new DiscoveryLookbackResolution(since, DiscoveryLookbackMode.Dynamic, latestSuccessful);
    }

    private async Task<DateTime?> GetLatestSuccessfulDiscoveryBeganAsync(CancellationToken cancellationToken)
    {
        DateTime? fromReports = null;
        try
        {
            fromReports = await discoveryResultsRepository.GetLatestDiscoveryBegan(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to query latest Discovery report DiscoveryBegan; trying published discovery-info.");
        }

        DateTime? fromPublishedInfo = null;
        try
        {
            var discoveryInfo = await discoveryInfoRepository.Get(cancellationToken);
            fromPublishedInfo = discoveryInfo?.LastSuccessfulDiscoveryBegan;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read published discovery-info for lastSuccessfulDiscoveryBegan.");
        }

        if (fromReports is null)
        {
            return fromPublishedInfo?.ToUniversalTime();
        }

        if (fromPublishedInfo is null)
        {
            return fromReports.Value.ToUniversalTime();
        }

        var reportUtc = fromReports.Value.ToUniversalTime();
        var infoUtc = fromPublishedInfo.Value.ToUniversalTime();
        return reportUtc > infoUtc ? reportUtc : infoUtc;
    }
}
