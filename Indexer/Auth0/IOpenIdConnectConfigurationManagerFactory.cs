using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Indexer.Auth0;

public interface IOpenIdConnectConfigurationManagerFactory
{
    IConfigurationManager<OpenIdConnectConfiguration> Create();
}