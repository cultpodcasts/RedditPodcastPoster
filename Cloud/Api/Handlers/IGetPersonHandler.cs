using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IGetPersonHandler
{
    Task<HttpResponseData> Handle(HttpRequestData req, string personName, ClientPrincipal? _, CancellationToken c);
}
