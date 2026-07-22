using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Public;

public interface IGetPublicEpisodeHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c);
}
