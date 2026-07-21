using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IPublicHandler
{
    Task<HttpResponseData> Get(
        HttpRequestData req, 
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        ClientPrincipal? _,
        CancellationToken c);
}