using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IPostPodcastHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        ClientPrincipal? clientPrincipal,
        CancellationToken c);
}
