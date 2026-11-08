using Azure.Core;
using Azure.Identity;
using Azure.Storage.Queues;
using Api.Models;

namespace Api.Services.Podcasts;

/// <summary>
/// Builds the Storage Queue client for kind-transfer enqueue. Local Azurite uses the
/// <c>AzureWebJobsStorage</c> connection string; production uses identity-based
/// <c>AzureWebJobsStorage__accountName</c> / <c>__clientId</c> (same split settings as the
/// <c>[QueueTrigger]</c> host binding).
/// </summary>
public static class PodcastKindTransferQueueClientFactory
{
    public static QueueClient Create()
    {
        const string queueName = PodcastKindTransferQueueMessage.QueueName;
        var connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (IsStorageConnectionString(connection))
        {
            return new QueueClient(connection, queueName, TriggerCompatibleOptions());
        }

        var accountName = Environment.GetEnvironmentVariable("AzureWebJobsStorage__accountName");
        if (string.IsNullOrWhiteSpace(accountName))
        {
            throw new InvalidOperationException(
                "AzureWebJobsStorage connection string or AzureWebJobsStorage__accountName is required for the podcast kind-transfer queue.");
        }

        var clientId = Environment.GetEnvironmentVariable("AzureWebJobsStorage__clientId");
        TokenCredential credential = string.IsNullOrWhiteSpace(clientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = clientId
            });

        return new QueueClient(
            new Uri($"https://{accountName}.queue.core.windows.net/{queueName}"),
            credential,
            TriggerCompatibleOptions());
    }

    /// <summary>
    /// The queue trigger decodes Base64 (host.json <c>extensions.queues.messageEncoding</c>).
    /// <see cref="QueueClient"/> defaults to raw text, and the host then poisons the message
    /// without running the function.
    /// </summary>
    internal static QueueClientOptions TriggerCompatibleOptions() =>
        new() { MessageEncoding = QueueMessageEncoding.Base64 };

    internal static bool IsStorageConnectionString(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("DefaultEndpointsProtocol=", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("UseDevelopmentStorage=", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("AccountName=", StringComparison.OrdinalIgnoreCase));
}
