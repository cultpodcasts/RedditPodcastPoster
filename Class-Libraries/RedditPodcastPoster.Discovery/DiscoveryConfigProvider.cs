using RedditPodcastPoster.PodcastServices.Abstractions;
using static RedditPodcastPoster.Discovery.DiscoveryConfig;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryServiceConfigProvider : IDiscoveryServiceConfigProvider
{
    private readonly ServiceConfig[] _spotifyConfigs =
    {
        new("Cults", DiscoverService.Spotify),
        new("Cult", DiscoverService.Spotify),
        new("Scientology", DiscoverService.Spotify),
        new("NXIVM", DiscoverService.Spotify),
        new("FLDS", DiscoverService.Spotify)
    };

    public IEnumerable<ServiceConfig> GetServiceConfigs(
        bool excludeSpotify, bool includeYouTube, bool includeListenNotes)
    {
        var serviceConfigs = new List<ServiceConfig>();
        if (!excludeSpotify)
        {
            serviceConfigs.AddRange(GetSpotifyServiceConfigs());
        }

        if (includeYouTube)
        {
            serviceConfigs.AddRange(GetYouTubeServiceConfigs());
        }

        if (includeListenNotes)
        {
            serviceConfigs.InsertRange(0, GetListenNotesServiceConfigs());
        }

        return serviceConfigs;
    }

    private IEnumerable<ServiceConfig> GetSpotifyServiceConfigs()
    {
        return _spotifyConfigs;
    }

    private IEnumerable<ServiceConfig> GetYouTubeServiceConfigs()
    {
        return new ServiceConfig[]
        {
            new("Cult", DiscoverService.YouTube)
        };
    }

    private IEnumerable<ServiceConfig> GetListenNotesServiceConfigs()
    {
        return new ServiceConfig[]
        {
            new("Cult", DiscoverService.ListenNotes)
        };
    }
}