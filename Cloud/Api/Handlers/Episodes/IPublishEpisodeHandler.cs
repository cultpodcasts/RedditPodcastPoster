using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Episodes;

public interface IPublishEpisodeHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        EpisodePublishRequestWrapper publishRequest,
        ClientPrincipal? cp,
        CancellationToken c);
}
