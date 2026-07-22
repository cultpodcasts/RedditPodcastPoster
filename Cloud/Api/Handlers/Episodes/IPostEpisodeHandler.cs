using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Episodes;

public interface IPostEpisodeHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c);
}
