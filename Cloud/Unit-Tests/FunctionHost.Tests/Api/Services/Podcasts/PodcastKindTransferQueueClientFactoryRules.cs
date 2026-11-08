using Azure.Storage.Queues;
using FluentAssertions;
using Api.Services.Podcasts;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastKindTransferQueueClientFactoryRules
{
    [Theory(DisplayName =
        "A value is treated as a Storage connection string when it carries Azurite or account-key connection " +
        "markers, so local.settings.json keeps using QueueClient(connectionString) while production MI settings do not.")]
    [InlineData("UseDevelopmentStorage=true", true)]
    [InlineData("DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=abc", true)]
    [InlineData("AccountName=cultpodcastsstg;AccountKey=abc;EndpointSuffix=core.windows.net", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("cultpodcastsstg", false)]
    public void connection_string_detection(string? value, bool expected)
    {
        // Arrange / Act
        var result = PodcastKindTransferQueueClientFactory.IsStorageConnectionString(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName =
        "The kind-transfer queue client encodes messages as Base64, because the queue trigger decodes Base64 " +
        "and a raw JSON body is poisoned before the worker runs.")]
    public void queue_client_uses_base64_for_the_trigger()
    {
        // Arrange / Act
        var options = PodcastKindTransferQueueClientFactory.TriggerCompatibleOptions();

        // Assert
        options.MessageEncoding.Should().Be(QueueMessageEncoding.Base64);
    }
}
