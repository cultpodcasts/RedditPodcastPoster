using System.Text.Json;
using Api.Models;
using Api.Services.Discovery;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Api.Functions;

/// <summary>
/// Background worker for <c>POST discovery-submissions/{id}/apply</c> and <c>/retry</c> (#1415): the request claims the
/// submission and runs the preflight, then this ingests and records the outcome on the submission, which
/// <c>GET discovery-submissions/{id}</c> already returns. Mirrors <see cref="PodcastKindTransferQueueFunction"/>.
/// </summary>
public class DiscoverySubmissionApplyQueueFunction(
    IDiscoverySubmissionReviewService reviewService,
    ILogger<DiscoverySubmissionApplyQueueFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("DiscoverySubmissionApplyQueue")]
    public async Task Run(
        [QueueTrigger(DiscoverySubmissionApplyQueueMessage.QueueName, Connection = "AzureWebJobsStorage")]
        string messageBody)
    {
        var message = Parse(messageBody, nameof(Run));
        if (message == null)
        {
            return;
        }

        logger.LogInformation("{method}: Starting queued {operation} of discovery submission '{submissionId}'.",
            nameof(Run), message.Operation, message.SubmissionId);
        var result = await reviewService.ProcessQueued(message, CancellationToken.None);
        logger.LogInformation("{method}: Queued {operation} of discovery submission '{submissionId}' finished: {status} ({submissionStatus}).",
            nameof(Run), message.Operation, message.SubmissionId, result.Status, result.SubmissionStatus);
    }

    /// <summary>
    /// After <c>maxDequeueCount</c> failed attempts the host moves the message here. Ends the claim so the submission
    /// is not left Applying with nothing running.
    /// </summary>
    [Function("DiscoverySubmissionApplyPoisonQueue")]
    public async Task RunPoison(
        [QueueTrigger(DiscoverySubmissionApplyQueueMessage.PoisonQueueName, Connection = "AzureWebJobsStorage")]
        string messageBody)
    {
        var message = Parse(messageBody, nameof(RunPoison));
        if (message == null)
        {
            return;
        }

        var result = await reviewService.AbandonPoisoned(message, CancellationToken.None);
        logger.LogWarning("{method}: Poisoned {operation} of discovery submission '{submissionId}': {status} ({submissionStatus}).",
            nameof(RunPoison), message.Operation, message.SubmissionId, result.Status, result.SubmissionStatus);
    }

    private DiscoverySubmissionApplyQueueMessage? Parse(string messageBody, string method)
    {
        DiscoverySubmissionApplyQueueMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<DiscoverySubmissionApplyQueueMessage>(messageBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "{method}: Invalid discovery-submission apply queue payload.", method);
            return null;
        }

        if (message is null || message.SubmissionId == Guid.Empty)
        {
            logger.LogError("{method}: Discovery-submission apply queue payload missing submissionId.", method);
            return null;
        }

        return message;
    }
}
