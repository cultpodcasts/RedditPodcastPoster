using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastKindTransferQueue
{
    Task EnqueueAsync(PodcastKindTransferQueueMessage message, CancellationToken cancellationToken);
}
