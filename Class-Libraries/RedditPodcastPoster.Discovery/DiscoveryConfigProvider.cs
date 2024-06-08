using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryServiceConfigProvider(
    IOptions<DiscoverySettings> discoverySettings,
    ILogger<DiscoveryServiceConfigProvider> logger)
    : IDiscoveryServiceConfigProvider
{
    private readonly DiscoverySettings _discoverySettings = discoverySettings.Value;

    public IEnumerable<ServiceConfig> GetServiceConfigs(
        bool excludeSpotify, bool includeYouTube, bool includeListenNotes)
    {
        logger.LogInformation(
            $"{nameof(GetServiceConfigs)}: {nameof(excludeSpotify)}= {excludeSpotify}, {nameof(includeYouTube)}= {includeYouTube}, {nameof(includeListenNotes)}= {includeListenNotes}. {_discoverySettings}");
        var serviceConfigs = new List<ServiceConfig>();
        if (_discoverySettings.Queries != null)
        {
            if (!excludeSpotify)
            {
                serviceConfigs.AddRange(
                    _discoverySettings.Queries.Where(x => x.DiscoverService == DiscoverService.Spotify));
            }

            if (includeYouTube)
            {
                serviceConfigs.AddRange(
                    _discoverySettings.Queries.Where(x => x.DiscoverService == DiscoverService.YouTube));
            }

            if (includeListenNotes)
            {
                serviceConfigs.InsertRange(0,
                    _discoverySettings.Queries.Where(x => x.DiscoverService == DiscoverService.ListenNotes));
            }
        }

        return serviceConfigs;
    }
}