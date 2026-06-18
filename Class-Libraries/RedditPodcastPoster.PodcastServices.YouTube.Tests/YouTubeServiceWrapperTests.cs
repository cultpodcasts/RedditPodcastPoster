using FluentAssertions;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubeServiceWrapperTests
{
    [Fact]
    public void Rotate_WalksFullIndexerRingBeforeExhausting()
    {
        var strategy = new Mock<IYouTubeApiKeyStrategy>();
        var ring = new List<ApplicationWrapper>
        {
            Wrap("key1", "Primary-1"),
            Wrap("key8", "Reattempt-1"),
            Wrap("key13", "Reattempt-2"),
            Wrap("key2", "Primary-2")
        };
        strategy.Setup(x => x.BuildIndexerKeyRing(0)).Returns(ring);
        strategy.Setup(x => x.GetApplication(ApplicationUsage.Indexer)).Returns(ring[0]);

        var wrapper = new YouTubeServiceWrapper(
            CreateService("key1"),
            ring[0],
            ApplicationUsage.Indexer,
            strategy.Object,
            ring,
            initialRingIndex: 0,
            indexerKeyStateService: null,
            NullLogger<YouTubeServiceWrapper>.Instance);

        wrapper.CurrentApplication.ApiKey.Should().Be("key1");
        wrapper.CanRotate.Should().BeTrue();

        wrapper.Rotate();
        wrapper.CurrentApplication.ApiKey.Should().Be("key8");
        wrapper.CanRotate.Should().BeTrue();

        wrapper.Rotate();
        wrapper.CurrentApplication.ApiKey.Should().Be("key13");
        wrapper.CanRotate.Should().BeTrue();

        wrapper.Rotate();
        wrapper.CurrentApplication.ApiKey.Should().Be("key2");
        wrapper.CanRotate.Should().BeTrue();

        var act = () => wrapper.Rotate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_WithInitialRingIndex_StartsAtSavedPosition()
    {
        var strategy = new Mock<IYouTubeApiKeyStrategy>();
        var ring = new List<ApplicationWrapper>
        {
            Wrap("key1", "Primary-1"),
            Wrap("key8", "Reattempt-1"),
            Wrap("key13", "Reattempt-2")
        };

        var wrapper = new YouTubeServiceWrapper(
            CreateService("key13"),
            ring[2],
            ApplicationUsage.Indexer,
            strategy.Object,
            ring,
            initialRingIndex: 2,
            indexerKeyStateService: null,
            NullLogger<YouTubeServiceWrapper>.Instance);

        wrapper.CurrentApplication.ApiKey.Should().Be("key13");
        wrapper.IndexerRingIndex.Should().Be(2);
    }

    [Fact]
    public async Task DisposeAsync_PersistsIndexerRingState()
    {
        var strategy = new Mock<IYouTubeApiKeyStrategy>();
        var ring = new List<ApplicationWrapper>
        {
            Wrap("key1", "Primary-1"),
            Wrap("key8", "Reattempt-1")
        };
        var stateService = new Mock<IYouTubeIndexerKeyStateService>();

        var wrapper = new YouTubeServiceWrapper(
            CreateService("key1"),
            ring[0],
            ApplicationUsage.Indexer,
            strategy.Object,
            ring,
            initialRingIndex: 0,
            stateService.Object,
            NullLogger<YouTubeServiceWrapper>.Instance);

        wrapper.Rotate();

        await wrapper.DisposeAsync();

        stateService.Verify(
            x => x.PersistSessionEndAsync(1, "key8", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ApplicationWrapper Wrap(string apiKey, string displayName) =>
        new(new Application
        {
            ApiKey = apiKey,
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = displayName
        }, 0, 2);

    private static YouTubeService CreateService(string apiKey) =>
        new(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "CultPodcasts"
        });
}
