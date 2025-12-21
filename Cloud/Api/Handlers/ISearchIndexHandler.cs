using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api.Handlers;

public interface ISearchIndexHandler
{
    Task<HttpResponseData> RunIndexer(HttpRequestData req, ClientPrincipal? cp, CancellationToken c);
}