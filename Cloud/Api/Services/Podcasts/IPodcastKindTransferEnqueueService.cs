using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastKindTransferEnqueueService
{
    Task<PodcastKindTransferEnqueueResult> EnqueueAsync(
        Guid podcastId,
        PodcastKindTransferRequest request,
        CancellationToken cancellationToken);
}
