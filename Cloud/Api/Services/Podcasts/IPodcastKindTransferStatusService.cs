using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastKindTransferStatusService
{
    Task<PodcastKindTransferJobStatusResult> GetAsync(Guid podcastId, CancellationToken cancellationToken);
}
