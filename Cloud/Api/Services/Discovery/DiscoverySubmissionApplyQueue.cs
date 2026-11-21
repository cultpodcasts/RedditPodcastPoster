using System.Text.Json;
using Api.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;

namespace Api.Services.Discovery;

/// <summary>
/// Sends a claimed discovery-submission apply / retry to <see cref="DiscoverySubmissionApplyQueueMessage.QueueName"/>
/// with the same storage account and Base64 encoding as the podcast kind-transfer queue. Registered with its own
/// <see cref="QueueClient"/> (the container's <see cref="QueueClient"/> singleton is the kind-transfer queue).
/// </summary>
public sealed class DiscoverySubmissionApplyQueue(
    QueueClient queueClient,
    ILogger<DiscoverySubmissionApplyQueue> logger) : IDiscoverySubmissionApplyQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(DiscoverySubmissionApplyQueueMessage message, CancellationToken cancellationToken)
    {
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var payload = JsonSerializer.Serialize(message, JsonOptions);
        await queueClient.SendMessageAsync(payload, cancellationToken);
        logger.LogInformation(
            "{method}: Enqueued {operation} of discovery submission '{submissionId}'.",
            nameof(EnqueueAsync),
            message.Operation,
            message.SubmissionId);
    }
}
