using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.TvShows;

public interface IGetTvShowEpisodeHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, Guid episodeId, CancellationToken c);
}
