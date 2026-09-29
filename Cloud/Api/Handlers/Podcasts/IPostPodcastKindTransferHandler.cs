using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Podcasts;

public interface IPostPodcastKindTransferHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastKindTransferCommand command,
        CancellationToken cancellationToken);
}
