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
            var token = await GetToken(config, request);
            if (token == null)
            {
                throw new InvalidOperationException("Failed to get token.");
            }

            return new SpotifyClient(config.WithToken(token.AccessToken));
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"Failure to create spotify-client. Response-status-code: '{ex.Response?.StatusCode.ToString() ?? "no-status-code"}', response-message: '{ex.Response?.Body ?? "empty-body"}'.");
            throw;
        }
    }

    private async Task<ClientCredentialsTokenResponse?> GetToken(
        SpotifyClientConfig config,
        ClientCredentialsRequest request)
    {
        const int maxTries = 3;
        var tr = 0;
        ClientCredentialsTokenResponse? token = null;
        var oAuthClient = new OAuthClient(config);
        while (token == null && tr < maxTries)
        {
            try
            {
                token = await oAuthClient.RequestToken(request);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to get spotify-oauth token. Try {tr} of {maxTries}.");
            }
            finally
            {
                tr++;
            }
        }

        return token;
    }
}