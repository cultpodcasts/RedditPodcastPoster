using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Public;

public interface IGetPublicEpisodeHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        CancellationToken c);
}
