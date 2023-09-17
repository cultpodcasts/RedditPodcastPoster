using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Indexer.Auth0;

public class OpenIdConnectConfigurationManagerFactory : IOpenIdConnectConfigurationManagerFactory
{
    private readonly Auth0Settings _auth0Settings;
    private readonly IDocumentRetriever _documentRetriever;
    private readonly ILogger<OpenIdConnectConfigurationManagerFactory> _logger;

    public OpenIdConnectConfigurationManagerFactory(
        IDocumentRetriever documentRetriever,
        IOptions<Auth0Settings> auth0Settings, 
        ILogger<OpenIdConnectConfigurationManagerFactory> logger)
    {
        _auth0Settings = auth0Settings.Value;
        _documentRetriever = documentRetriever;
        _logger = logger;
    }

    public IConfigurationManager<OpenIdConnectConfiguration> Create()
    {
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_auth0Settings.Issuer}.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            _documentRetriever
        );
    }
}