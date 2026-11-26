using Api.Models;

namespace Api.Services.Discovery;

public interface IDiscoverySubmissionApplyQueue
{
    Task EnqueueAsync(DiscoverySubmissionApplyQueueMessage message, CancellationToken cancellationToken);
}
