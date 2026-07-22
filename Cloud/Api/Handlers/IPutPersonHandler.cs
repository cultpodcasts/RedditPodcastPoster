using Microsoft.Azure.Functions.Worker.Http;
using PersonDto = Api.Dtos.Person;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IPutPersonHandler
{
    Task<HttpResponseData> Handle(HttpRequestData req, PersonDto person, ClientPrincipal? _, CancellationToken ct);
}
