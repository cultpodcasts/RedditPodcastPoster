using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Auth0;

public class SigningKeysFactory(IOptions<Auth0ValidationOptions> auth0ValidationOptions) : ISigningKeysFactory
{
    private readonly Auth0ValidationOptions _options = auth0ValidationOptions.Value ??
                                                       throw new ArgumentNullException(
                                                           $"Missing '{nameof(Auth0ValidationOptions)}'.");

    // Implement IAsyncFactory<T>.Create()
    async Task<ICollection<SecurityKey>?> IAsyncFactory<ICollection<SecurityKey>?>.Create()
    {
        var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"https://{_options.Domain}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

        var discoveryDocument = await configurationManager.GetConfigurationAsync();
        var signingKeys = discoveryDocument.SigningKeys;
        return signingKeys;
    }
}