using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api.Handlers;

public interface IPublicHandler
{
    Task<HttpResponseData> Get(
        HttpRequestData req, 
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        ClientPrincipal? _,
        CancellationToken c);
}