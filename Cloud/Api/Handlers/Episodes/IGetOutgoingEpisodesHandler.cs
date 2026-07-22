using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Episodes;

public interface IGetOutgoingEpisodesHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        ClientPrincipal? cp,
        CancellationToken c);
}
