using Microsoft.Extensions.Logging;
using Api.Models;

namespace Api.Services.Podcasts;

/// <summary>
/// Thin orchestration seam for the queue-triggered worker. Keeps the Functions host free of transfer
/// phase logic so rules can assert MarkRunning → Transfer → ApplyResult without spinning the host.
/// </summary>
public class PodcastKindTransferQueueProcessor(
    IPodcastKindTransferService transferService,
    IPodcastKindTransferJobUpdater jobUpdater,
    ILogger<PodcastKindTransferQueueProcessor> logger) : IPodcastKindTransferQueueProcessor
{
    public async Task ProcessAsync(
        PodcastKindTransferQueueMessage message,
        CancellationToken cancellationToken)
    {
        await jobUpdater.MarkRunningAsync(message.PodcastId, message.TargetKind, message.EnqueuedUtc);

        try
        {
            var result = await transferService.TransferAsync(
                message.PodcastId,
                new PodcastKindTransferRequest { TargetKind = message.TargetKind },
                cancellationToken);

            await jobUpdater.ApplyResultAsync(result, message.EnqueuedUtc);

            logger.LogInformation(
                "{method}: Queued kind transfer for podcast '{podcastId}' finished with {status} ({playableCount} playables).",
                nameof(ProcessAsync),
                message.PodcastId,
                result.Status,
                result.PlayableCount);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{method}: Kind transfer for podcast '{podcastId}' threw; marking job Failed.",
                nameof(ProcessAsync),
                message.PodcastId);

            await jobUpdater.ApplyResultAsync(
                new PodcastKindTransferResult(
                    PodcastKindTransferStatus.Failed,
                    message.PodcastId,
                    message.TargetKind),
                message.EnqueuedUtc);

            throw;
        }
    }
}
