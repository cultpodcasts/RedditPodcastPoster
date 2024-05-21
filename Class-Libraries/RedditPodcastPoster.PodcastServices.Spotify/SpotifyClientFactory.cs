using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyClientFactory(
    IOptions<SpotifySettings> settings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SpotifyClientFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISpotifyClientFactory
{
    private readonly SpotifySettings _settings = settings.Value;

    public async Task<ISpotifyClient> Create()
    {
        var config = SpotifyClientConfig.CreateDefault();

        logger.LogInformation(
            $"{nameof(Create)}: Using spotify client-id ending '{_settings.ClientId.Substring(_settings.ClientId.Length - 2)}' and client-secret ending with '{_settings.ClientSecret.Substring(_settings.ClientSecret.Length - 2)}'.");

        var request = new ClientCredentialsRequest(_settings.ClientId, _settings.ClientSecret);
        try
        {
            var response = await new OAuthClient(config).RequestToken(request);

            return new SpotifyClient(config.WithToken(response.AccessToken));
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"Failure to create spotify-client. Response-status-code: '{ex.Response?.StatusCode.ToString() ?? "no-status-code"}', response-message: '{ex.Response?.Body ?? "empty-body"}'.");
            throw;
        }
    }
}