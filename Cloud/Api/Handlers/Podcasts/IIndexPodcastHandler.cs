using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Podcasts;

public interface IIndexPodcastHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        string podcastName,
        ClientPrincipal? clientPrincipal,
        CancellationToken c);
}
