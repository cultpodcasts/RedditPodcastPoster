using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Episodes;

public interface IPostEpisodeYouTubeAlternateAttachHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeYouTubeAlternateAttachCommand command,
        CancellationToken cancellationToken);
}
