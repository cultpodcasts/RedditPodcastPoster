using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Api.Auth;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class ClientPrincipalFactory(ILogger<ClientPrincipalFactory> logger) : IClientPrincipalFactory
{
    private const string ClaimsRolesIdentifierType = "https://api.cultpodcasts.com/roles";
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
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var decodedBearer = handler.ReadJwtToken(bearer);

            var permissions = decodedBearer.Claims.Where(x => x.Type == "permissions")
                .Select(x => new ClientPrincipalClaim {Type = "permissions", Value = x.Value});
            var roles = decodedBearer.Claims.Where(x => x.Type == ClaimsRolesIdentifierType)
                .Select(x => new ClientPrincipalClaim {Type = ClaimsRolesIdentifierType, Value = x.Value});
            var audiences = decodedBearer.Claims.Where(x => x.Type == "aud")
                .Select(x => new ClientPrincipalClaim {Type = "aud", Value = x.Value});
            var scopes = decodedBearer.Claims.Where(x => x.Type == "scope")
                .Select(x => new ClientPrincipalClaim {Type = "scope", Value = x.Value});
            var azps = decodedBearer.Claims.Where(x => x.Type == "azp")
                .Select(x => new ClientPrincipalClaim {Type = "azp", Value = x.Value});

            return new ClientPrincipal
            {
                Claims = permissions
                    .Concat(roles)
                    .Concat([new ClientPrincipalClaim {Type = "iss", Value = decodedBearer.Issuer}])
                    .Concat([
                        new ClientPrincipalClaim
                        {
                            Type = ClientPrincipal.ClaimsNameIdentifierType,
                            Value = decodedBearer.Subject
                        }
                    ])
                    .Concat(audiences)
                    .Concat([
                        new ClientPrincipalClaim
                        {
                            Type = "iat",
                            Value = new DateTimeOffset(decodedBearer.IssuedAt.ToUniversalTime()).ToUnixTimeSeconds()
                                .ToString()
                        }
                    ])
                    .Concat([
                        new ClientPrincipalClaim
                        {
                            Type = "exp",
                            Value = new DateTimeOffset(decodedBearer.ValidTo.ToUniversalTime()).ToUnixTimeSeconds()
                                .ToString()
                        }
                    ])
                    .Concat(scopes)
                    .Concat(azps)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to decode jwt token");
            return null;
        }
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