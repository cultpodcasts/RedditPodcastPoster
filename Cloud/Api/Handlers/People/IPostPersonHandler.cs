using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.People;

public interface IPostPersonHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        PersonChangeRequestWrapper request,
        ClientPrincipal? _,
        CancellationToken c);
}
