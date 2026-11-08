using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.Podcasts;

namespace Api.Functions;

/// <summary>
/// Background worker for <c>POST podcast/{id}/kind</c>. Runs the existing transfer service under the
/// single-flight LookUps marker and updates the job document for <c>GET podcast/{id}/kind</c>.
/// </summary>
public class PodcastKindTransferQueueFunction(
    IPodcastKindTransferQueueProcessor processor,
    ILogger<PodcastKindTransferQueueFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("PodcastKindTransferQueue")]
    public async Task Run(
        [QueueTrigger(PodcastKindTransferQueueMessage.QueueName, Connection = "AzureWebJobsStorage")]
        string messageBody)
    {
        PodcastKindTransferQueueMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<PodcastKindTransferQueueMessage>(messageBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "{method}: Invalid kind-transfer queue payload.", nameof(Run));
            return;
        }

        if (message is null || message.PodcastId == Guid.Empty)
        {
            logger.LogError("{method}: Kind-transfer queue payload missing podcastId.", nameof(Run));
            return;
        }

        logger.LogInformation(
            "{method}: Starting queued kind transfer for podcast '{podcastId}' to {targetKind}.",
            nameof(Run),
            message.PodcastId,
            message.TargetKind);

        await processor.ProcessAsync(message, CancellationToken.None);
    }
}
