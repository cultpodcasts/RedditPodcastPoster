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

    public DiscoveryConfig CreateDiscoveryConfig(GetServiceConfigOptions options)
    {
        logger.LogInformation(
            $"{nameof(CreateDiscoveryConfig)}: {options} {_discoverySettings}");
        var serviceConfigs = new List<ServiceConfig>();
        if (_discoverySettings.Queries != null)
        {
            if (!options.ExcludeSpotify)
            {
                serviceConfigs.AddRange(
                    _discoverySettings.Queries.Where(x => x.DiscoverService == DiscoverService.Spotify));
            }

            if (options.IncludeYouTube)
            {
                serviceConfigs.AddRange(
                    _discoverySettings.Queries.Where(x => x.DiscoverService == DiscoverService.YouTube));
            }

            if (options.IncludeListenNotes)
            {
                serviceConfigs.InsertRange(0,
                    _discoverySettings.Queries.Where(x => x.DiscoverService == DiscoverService.ListenNotes));
            }

            if (options.IncludeTaddy)
            {
                serviceConfigs.InsertRange(0,
                    _discoverySettings.Queries.Where(x => x.DiscoverService == DiscoverService.Taddy));
            }
        }

        return new DiscoveryConfig(options.Since, options.TaddyOffset, serviceConfigs, options.EnrichFromSpotify,
            options.EnrichFromApple);
    }
}