using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Factories;

public class SpotifyClientConfigFactory(
    IOptions<SpotifySettings> settings,
    ILogger<SpotifyClientConfigFactory> logger
) : ISpotifyClientConfigFactory
{
    private readonly SpotifySettings _settings = settings.Value;

    public async Task<SpotifyClientConfig> Create()
    {
        logger.LogInformation(
            "{method}: Using spotify client-id ending '{clientIdEnding}' and client-secret ending with '{secretEnding}'.",
            nameof(Create),
            _settings.ClientId[^2..],
            _settings.ClientSecret[^2..]
        );

        var config = SpotifyClientConfig.CreateDefault();

        var token = await GetToken(config);
        if (token == null)
        {
            throw new InvalidOperationException("Failed to get token.");
        }

        return config.WithToken(token.AccessToken);
    }

    private async Task<ClientCredentialsTokenResponse?> GetToken(
        SpotifyClientConfig config)
    {
        const int maxTries = 3;
        var request = new ClientCredentialsRequest(_settings.ClientId, _settings.ClientSecret);
        var oAuthClient = new OAuthClient(config);

        var tr = 0;
        ClientCredentialsTokenResponse? token = null;
        while (token == null && tr < maxTries)
        {
            try
            {
                token = await oAuthClient.RequestToken(request);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get spotify-oauth token. Try {try} of {maxTries}.", tr, maxTries);
            }
            finally
            {
                tr++;
            }
        }

        return token;
    }
}