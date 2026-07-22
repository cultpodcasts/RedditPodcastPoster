using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Podcasts;

public interface IRenamePodcastHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastRenameCommand change,
        ClientPrincipal? clientPrincipal,
        CancellationToken c);
}
