using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace RedditPodcastPoster.Auth0;

public class Auth0TokenValidator(
    ISigningKeysFactory signingKeysFactory,
    IOptions<Auth0ValidationOptions> options,
    ILogger<Auth0TokenValidator> logger) : IAuth0TokenValidator
{
    private readonly Auth0ValidationOptions _options =
        options.Value ?? throw new ArgumentNullException($"Missing '{nameof(Auth0ValidationOptions)}'");

    public ICollection<SecurityKey>? SecurityKeys { get; set; } =
        signingKeysFactory.GetSecurityKeys().GetAwaiter().GetResult();

    public ValidatedToken? GetClaimsPrincipal(string auth0Bearer)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = SecurityKeys,
            ValidateLifetime = true
        };
        var handler = new JwtSecurityTokenHandler();
        var claimsPrincipal = handler.ValidateToken(auth0Bearer, validationParameters, out var token);
        if (claimsPrincipal == null || token == null)
        {
            logger.LogWarning(
                $"Unable to validate token. Claim-principal null: {claimsPrincipal == null}. Validated-token null: {token == null}.");
            return null;
        }

        logger.LogInformation("Token valid.");
        return new ValidatedToken(claimsPrincipal, token);
    }
}