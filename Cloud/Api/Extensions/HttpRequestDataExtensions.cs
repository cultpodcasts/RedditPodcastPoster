using System.Text;
using System.Text.Json;
using Api.Auth;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Extensions;

public static class HttpRequestDataExtensions
{
    private const string Bearer = "Bearer ";

    public static ClientPrincipal? GetClientPrincipal(this HttpRequestData request)
    {
        var auth = request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out var claims);
        if (auth)
        {
            return GetAppServiceAuthClientPrincipal(claims);
        }

        auth = request.Headers.TryGetValues("Authorization", out claims);
        if (auth)
        {
            return GetAuth0ClientPrincipal(claims);
        }

        return null;
    }

    private static ClientPrincipal? GetAuth0ClientPrincipal(IEnumerable<string>? claims)
    {
        claims = claims
            .Where(x => x.StartsWith(Bearer))
            .Select(x => x.Substring(Bearer.Length))
            .Select(x => x.Split(".")[1])
            .ToArray();
        try
        {
            var claimHeader = claims!.First();
            var decoded = Convert.FromBase64String(claimHeader);
            var json = Encoding.UTF8.GetString(decoded);
            var jwtToken = JsonSerializer.Deserialize<Auth0Payload>(json,
                new JsonSerializerOptions {PropertyNameCaseInsensitive = true});

            var permissions = jwtToken.Permissions.Select(x => new ClientPrincipalClaim
                {Type = "permissions", Value = x});
            var roles = jwtToken.Roles.Select(x => new ClientPrincipalClaim
                {Type = ClientPrincipal.ClaimsRolesIdentifierType, Value = x});
            var audiences = jwtToken.Audience.Select(x => new ClientPrincipalClaim
                {Type = "aud", Value = x});

            return new ClientPrincipal
            {
                Claims = permissions
                    .Concat(roles)
                    .Concat([new ClientPrincipalClaim {Type = "iss", Value = jwtToken.Issuer}])
                    .Concat([
                        new ClientPrincipalClaim
                        {
                            Type = ClientPrincipal.ClaimsNameIdentifierType,
                            Value = jwtToken.Subject
                        }
                    ])
                    .Concat(audiences)
                    .Concat([
                        new ClientPrincipalClaim
                            {Type = "iat", Value = jwtToken.IssuedAtTimeSeconds?.ToString() ?? string.Empty}
                    ])
                    .Concat([
                        new ClientPrincipalClaim
                            {Type = "exp", Value = jwtToken.ExpirationTimeSeconds?.ToString() ?? string.Empty}
                    ])
                    .Concat([new ClientPrincipalClaim {Type = "scope", Value = jwtToken.Scope}])
                    .Concat([new ClientPrincipalClaim {Type = "azp", Value = jwtToken.Azp}])
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ClientPrincipal? GetAppServiceAuthClientPrincipal(IEnumerable<string>? claims)
    {
        try
        {
            var claimHeader = claims!.First();
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