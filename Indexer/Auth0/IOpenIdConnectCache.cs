using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Indexer.Auth0;

public interface IOpenIdConnectCache
{
    Task Save(OpenIdConnectConfiguration openIdConnect);
    Task<OpenIdConnectConfiguration> Get();
    Task Delete();
}