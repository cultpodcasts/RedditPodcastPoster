using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RedditPodcastPoster.Auth0.Configuration;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Auth0.Validators;

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
        var trustedIssuers = _options.GetTrustedIssuers();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = trustedIssuers,
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

            logger.LogInformation(
                "Token valid. TrustedIssuers={TrustedIssuerCount}, TrustStagingIssuer={TrustStagingIssuer}.",
                trustedIssuers.Count,
                _options.TrustsStagingIssuer);
            return new ValidatedToken(claimsPrincipal, token);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failure validating token.");
        }

        return null;
    }
}
