using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;
using PersonDto = Api.Dtos.Person;

namespace Api.Handlers;

public interface IPersonHandler
{
    Task<HttpResponseData> GetAll(HttpRequestData req, ClientPrincipal? _, CancellationToken c);
    Task<HttpResponseData> Get(HttpRequestData req, string personName, ClientPrincipal? _, CancellationToken c);
    Task<HttpResponseData> Post(HttpRequestData req, PersonChangeRequestWrapper request, ClientPrincipal? _, CancellationToken c);
    Task<HttpResponseData> Put(HttpRequestData req, PersonDto person, ClientPrincipal? _, CancellationToken ct);
}
