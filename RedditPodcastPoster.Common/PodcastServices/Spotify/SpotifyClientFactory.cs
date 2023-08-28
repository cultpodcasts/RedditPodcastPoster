using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyClientFactory : ISpotifyClientFactory
{
    private readonly ILogger<SpotifyClientFactory> _logger;
    private readonly SpotifySettings _settings;

    public SpotifyClientFactory(IOptions<SpotifySettings> settings, ILogger<SpotifyClientFactory> logger)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<ISpotifyClient> Create()
    {
        var config = SpotifyClientConfig.CreateDefault();

        var request = new ClientCredentialsRequest(_settings.ClientId, _settings.ClientSecret);
        var response = await new OAuthClient(config).RequestToken(request);

        return new SpotifyClient(config.WithToken(response.AccessToken));
    }

    public static IServiceCollection AddSpotifyClient(IServiceCollection services)
    {
        return services
            .AddScoped<ISpotifyClientFactory, SpotifyClientFactory>()
            .AddScoped(s => s.GetService<ISpotifyClientFactory>()!.Create().GetAwaiter().GetResult());
    }
}