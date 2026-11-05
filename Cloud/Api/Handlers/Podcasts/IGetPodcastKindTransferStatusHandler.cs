using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Podcasts;

public interface IGetPodcastKindTransferStatusHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid podcastId,
        CancellationToken cancellationToken);
}
