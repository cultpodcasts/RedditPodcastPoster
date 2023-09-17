using Microsoft.IdentityModel.Protocols;

namespace Indexer.Auth0;

public interface IHttpDocumentRetrieverFactory
{
    IDocumentRetriever Create();
}