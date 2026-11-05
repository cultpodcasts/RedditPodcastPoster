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
}
