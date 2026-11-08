using Api.Models;

namespace Api.Services.Podcasts;

/// <summary>Runs one queued kind-transfer message: MarkRunning → Transfer → ApplyResult.</summary>
public interface IPodcastKindTransferQueueProcessor
{
    Task ProcessAsync(PodcastKindTransferQueueMessage message, CancellationToken cancellationToken);
}
