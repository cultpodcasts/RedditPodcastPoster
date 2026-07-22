using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IRunSearchIndexHandler
{
    Task<HttpResponseData> Handle(HttpRequestData req, ClientPrincipal? cp, CancellationToken c);
}
