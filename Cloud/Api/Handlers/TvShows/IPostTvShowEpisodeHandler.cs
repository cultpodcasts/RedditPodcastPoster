using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.TvShows;

public interface IPostTvShowEpisodeHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TvShowEpisodeChangeRequestWrapper request,
        CancellationToken c);
}
