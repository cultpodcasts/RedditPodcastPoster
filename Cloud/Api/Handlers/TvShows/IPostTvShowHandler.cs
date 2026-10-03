using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.TvShows;

public interface IPostTvShowHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TvShowChangeRequestWrapper request,
        CancellationToken c);
}
