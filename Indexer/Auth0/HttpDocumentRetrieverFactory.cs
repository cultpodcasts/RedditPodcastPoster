using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;

namespace Indexer.Auth0;

public class HttpDocumentRetrieverFactory : IHttpDocumentRetrieverFactory
{
    private readonly Auth0Settings _auth0Settings;
    private readonly ILogger<HttpDocumentRetrieverFactory> _logger;

    public HttpDocumentRetrieverFactory(IOptions<Auth0Settings> auth0Settings,
        ILogger<HttpDocumentRetrieverFactory> logger)
    {
        _auth0Settings = auth0Settings.Value;
        _logger = logger;
    }

    public IDocumentRetriever Create()
    {
        return new HttpDocumentRetriever {RequireHttps = _auth0Settings.Issuer.StartsWith("https://")};
    }
}