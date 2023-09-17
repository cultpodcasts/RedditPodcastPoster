using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Indexer.Auth0;

public class TokenValidator : ITokenValidator
{
    private readonly Auth0Settings _auth0Settings;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _openIdConnectConfigurationManager;
    private readonly ILogger<TokenValidator> _logger;

    public TokenValidator(IOptions<Auth0Settings> auth0Settings,
        IConfigurationManager<OpenIdConnectConfiguration> openIdConnectConfigurationManager,
        ILogger<TokenValidator> logger)
    {
        _openIdConnectConfigurationManager = openIdConnectConfigurationManager;
        _logger = logger;
        _auth0Settings = auth0Settings.Value;
    }

    public async Task<ClaimsPrincipal> ValidateTokenAsync(AuthenticationHeaderValue value)
    {
        if (value?.Scheme != "Bearer")
        {
            return null;
        }

        var config = await _openIdConnectConfigurationManager.GetConfigurationAsync(CancellationToken.None);

        var validationParameter = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidAudience = _auth0Settings.Audience,
            ValidateAudience = true,
            ValidIssuer = _auth0Settings.Issuer,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            IssuerSigningKeys = config.SigningKeys
        };

        ClaimsPrincipal result = null;
        var tries = 0;

        while (result == null && tries <= 1)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                result = handler.ValidateToken(value.Parameter, validationParameter, out var token);
            }
            catch (SecurityTokenSignatureKeyNotFoundException ex1)
            {
                // This exception is thrown if the signature key of the JWT could not be found.
                // This could be the case when the issuer changed its signing keys, so we trigger a 
                // refresh and retry validation.
                _openIdConnectConfigurationManager.RequestRefresh();
                tries++;
            }
            catch (SecurityTokenException ex2)
            {
                return null;
            }
        }

        return result;
    }
}