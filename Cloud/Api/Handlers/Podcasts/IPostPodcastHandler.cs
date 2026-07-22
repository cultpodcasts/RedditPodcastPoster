using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Podcasts;

public interface IPostPodcastHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        CancellationToken c);
}
