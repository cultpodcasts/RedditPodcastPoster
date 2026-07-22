using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Podcasts;

public interface IRenamePodcastHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastRenameCommand change,
        CancellationToken c);
}
