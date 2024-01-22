using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyClientFactory(IOptions<SpotifySettings> settings, ILogger<SpotifyClientFactory> logger)
    : ISpotifyClientFactory
{
    private readonly SpotifySettings _settings = settings.Value;

    public async Task<ISpotifyClient> Create()
    {
        var config = SpotifyClientConfig.CreateDefault();

        var request = new ClientCredentialsRequest(_settings.ClientId, _settings.ClientSecret);
        var response = await new OAuthClient(config).RequestToken(request);

        return new SpotifyClient(config.WithToken(response.AccessToken));
    }
}