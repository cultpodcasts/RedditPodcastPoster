using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.TvShows;

public interface IDeleteTvShowEpisodeHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid episodeId,
        CancellationToken c);
}
