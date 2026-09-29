using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastKindTransferService
{
    Task<PodcastKindTransferResult> TransferAsync(
        Guid podcastId,
        PodcastKindTransferRequest request,
        CancellationToken cancellationToken);
}
