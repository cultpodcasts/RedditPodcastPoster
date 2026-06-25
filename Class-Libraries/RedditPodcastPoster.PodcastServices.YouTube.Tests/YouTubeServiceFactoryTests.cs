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
    [Fact]
    public void Create_ForBluesky_UsesApiKeyStrategyInsteadOfIndexerRing()
    {
        var blueskyApplication = new ApplicationWrapper(
            new Application
            {
                ApiKey = "bluesky-key",
                Name = "BlueskyApp",
                DisplayName = "ApiKey-7 - Bluesky",
                Usage = ApplicationUsage.Bluesky
            },
            Index: 0,
            Reattempts: 0);

        var strategy = new Mock<IYouTubeApiKeyStrategy>();
        strategy.Setup(x => x.GetApplication(ApplicationUsage.Bluesky)).Returns(blueskyApplication);

        var serviceProvider = new Mock<IServiceProvider>();

        var factory = new YouTubeServiceFactory(
            strategy.Object,
            serviceProvider.Object,
            NullLogger<YouTubeServiceFactory>.Instance,
            NullLogger<YouTubeServiceWrapper>.Instance);

        var wrapper = factory.Create(ApplicationUsage.Bluesky);

        wrapper.CurrentApplication.ApiKey.Should().Be("bluesky-key");
        wrapper.Usage.Should().Be(ApplicationUsage.Bluesky);
        strategy.Verify(x => x.GetApplication(ApplicationUsage.Bluesky), Times.Once);
    }
}
