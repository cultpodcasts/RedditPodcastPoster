using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubeServiceFactoryTests
{
    [Theory]
    [InlineData(ApplicationUsage.Api, "api-key", "ApiKey-12 - Api")]
    [InlineData(ApplicationUsage.Bluesky, "bluesky-key", "ApiKey-7 - Bluesky")]
    [InlineData(ApplicationUsage.Cli, "cli-key", "ApiKey-3 - Cli")]
    [InlineData(ApplicationUsage.Discover, "discover-key", "ApiKey-5 - Discover")]
    public void Create_ForNonIndexerUsage_UsesApiKeyStrategyInsteadOfIndexerRing(
        ApplicationUsage usage,
        string apiKey,
        string displayName)
    {
        var applicationWrapper = new ApplicationWrapper(
            new Application
            {
                ApiKey = apiKey,
                Name = $"{usage}App",
                DisplayName = displayName,
                Usage = usage
            },
            Index: 0,
            Reattempts: 0);

        var strategy = new Mock<IYouTubeApiKeyStrategy>();
        strategy.Setup(x => x.GetApplication(usage)).Returns(applicationWrapper);

        var serviceProvider = new Mock<IServiceProvider>();

        var factory = new YouTubeServiceFactory(
            strategy.Object,
            serviceProvider.Object,
            NullLogger<YouTubeServiceFactory>.Instance,
            NullLogger<YouTubeServiceWrapper>.Instance);

        var wrapper = factory.Create(usage);

        wrapper.CurrentApplication.ApiKey.Should().Be(apiKey);
        wrapper.Usage.Should().Be(usage);
        strategy.Verify(x => x.GetApplication(usage), Times.Once);
        strategy.Verify(x => x.BuildIndexerKeyRing(It.IsAny<int>()), Times.Never);
    }
}
