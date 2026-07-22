using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Podcasts;

public interface IGetPodcastHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastGetRequest podcastGetRequest,
        CancellationToken c);
}
