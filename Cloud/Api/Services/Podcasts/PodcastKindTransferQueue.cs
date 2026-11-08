using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Api.Models;

namespace Api.Services.Podcasts;

public sealed class PodcastKindTransferQueue(
    QueueClient queueClient,
    ILogger<PodcastKindTransferQueue> logger) : IPodcastKindTransferQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(PodcastKindTransferQueueMessage message, CancellationToken cancellationToken)
    {
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var payload = JsonSerializer.Serialize(message, JsonOptions);
        await queueClient.SendMessageAsync(payload, cancellationToken);
        logger.LogInformation(
            "{method}: Enqueued kind transfer for podcast '{podcastId}' to {targetKind}.",
            nameof(EnqueueAsync),
            message.PodcastId,
            message.TargetKind);
    }
}
