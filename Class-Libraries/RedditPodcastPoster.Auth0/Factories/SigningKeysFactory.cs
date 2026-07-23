using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RedditPodcastPoster.Auth0.Configuration;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Auth0.Factories;

public class SigningKeysFactory(IOptions<Auth0ValidationOptions> auth0ValidationOptions) : ISigningKeysFactory
{
    private readonly Auth0ValidationOptions _options = auth0ValidationOptions.Value ??
                                                       throw new ArgumentNullException(
                                                           $"Missing '{nameof(Auth0ValidationOptions)}'.");

    public async Task<ICollection<SecurityKey>?> Create()
    {
        var keys = new List<SecurityKey>();
        foreach (var domain in _options.GetTrustedDomains())
        {
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"https://{domain}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            var discoveryDocument = await configurationManager.GetConfigurationAsync();
            keys.AddRange(discoveryDocument.SigningKeys);
        }

        return keys;
    }
}
