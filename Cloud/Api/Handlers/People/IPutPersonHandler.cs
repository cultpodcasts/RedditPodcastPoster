using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.People;

public interface IPutPersonHandler
{
    Task<HttpResponseData> Handle(HttpRequestData req, PersonChangeRequest person, ClientPrincipal? _, CancellationToken ct);
}
