using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Podcasts;

public interface IPostPodcastHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        ClientPrincipal? clientPrincipal,
        CancellationToken c);
}
