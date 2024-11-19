using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;

namespace Api;

public class ClientPrincipalFactory(
    IAuth0TokenValidator auth0TokenValidator,
    ILogger<ClientPrincipalFactory> logger) : IClientPrincipalFactory
{
    private const string Bearer = "Bearer ";

    public ClientPrincipal? Create(HttpRequestData request)
    {
        var auth = request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out var claims);
        if (auth)
        {
            if (claims != null && claims.Any())
            {
                logger.LogInformation("Has X-MS-CLIENT-PRINCIPAL header.");
                return GetAppServiceAuthClientPrincipal(claims);
            }

            logger.LogError("Has X-MS-CLIENT-PRINCIPAL header but no claims.");
        }

        auth = request.Headers.TryGetValues("Authorization", out claims);
        if (auth)
        {
            if (claims != null && claims.Any())
            {
                logger.LogInformation("Has Authorization header.");
                return GetAuth0ClientPrincipal(claims);
            }

            logger.LogError("Has Authorization header but no claims.");
        }

        return null;
    }

    private ClientPrincipal? GetAuth0ClientPrincipal(IEnumerable<string> claims)
    {
        var bearer = claims
            .Where(x => x.StartsWith(Bearer))
            .Select(x => x.Substring(Bearer.Length))
            .First();

        var validatedToken = auth0TokenValidator.GetClaimsPrincipal(bearer);
        if (validatedToken == null)
        {
            logger.LogWarning($"No client-principal.");
            return null;
        }

        return validatedToken.ToClientPrincipal();
    }

    private ClientPrincipal? GetAppServiceAuthClientPrincipal(IEnumerable<string> claims)
    {
        try
        {
            var claimHeader = claims.First();
            var decoded = Convert.FromBase64String(claimHeader);
            var json = Encoding.UTF8.GetString(decoded);
            var principal = JsonSerializer.Deserialize<ClientPrincipal>(json,
                new JsonSerializerOptions {PropertyNameCaseInsensitive = true});
            return principal;
        }
        catch (Exception)
        {
            return null;
        }
    }
}