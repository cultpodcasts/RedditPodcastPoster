using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.TvShows;

public interface IGetTvShowHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, string identifier, CancellationToken c);
}
