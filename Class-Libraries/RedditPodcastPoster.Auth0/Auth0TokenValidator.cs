using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Auth0;

public class Auth0TokenValidator(
    IAsyncInstance<ICollection<SecurityKey>?> securityKeysProvider,
    IOptions<Auth0ValidationOptions> options,
    ILogger<Auth0TokenValidator> logger) : IAuth0TokenValidator
{
    private readonly Auth0ValidationOptions _options =
        options.Value ?? throw new ArgumentNullException($"Missing '{nameof(Auth0ValidationOptions)}'");

    public async Task<ValidatedToken?> GetClaimsPrincipalAsync(string auth0Bearer)
    {
        var securityKeys = await securityKeysProvider.GetAsync();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = securityKeys,
            ValidateLifetime = true
        };
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var claimsPrincipal = handler.ValidateToken(auth0Bearer, validationParameters, out var token);
            if (claimsPrincipal == null || token == null)
            {
                logger.LogWarning(
                    "Unable to validate token. Claim-principal null: {B}. Validated-token null: {B1}.", claimsPrincipal == null, token == null);
                return null;
            }

            logger.LogInformation("Token valid.");
            return new ValidatedToken(claimsPrincipal, token);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failure validating token.");
        }

        return null;
    }
}