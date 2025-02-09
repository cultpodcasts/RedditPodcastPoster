using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Factories;

public class SpotifyClientFactory(
    ISpotifyClientConfigFactory configFactory,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SpotifyClientFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISpotifyClientFactory
{
    public async Task<ISpotifyClient> Create()
    {
        var config = await configFactory.Create();
        if (config == null)
        {
            throw new InvalidOperationException("Failed to create spotify client config.");
        }

        return new SpotifyClient(config);
    }
}