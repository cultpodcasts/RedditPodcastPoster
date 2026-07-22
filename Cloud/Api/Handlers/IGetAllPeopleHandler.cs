using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IGetAllPeopleHandler
{
    Task<HttpResponseData> Handle(HttpRequestData req, ClientPrincipal? _, CancellationToken c);
}
