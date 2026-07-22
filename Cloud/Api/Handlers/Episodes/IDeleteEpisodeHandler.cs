using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IDeleteEpisodeHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        CancellationToken c);
}
