using FluentAssertions;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubeServiceWrapperTests
{
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName = "When the indexer key ring has several keys, Rotate walks the full ring before throwing, because each key must be tried once.")]
    public void Rotate_WalksFullIndexerRingBeforeExhausting()
    {
        // Arrange
        var strategy = _mocker.GetMock<IYouTubeApiKeyStrategy>();
        var ring = new List<ApplicationWrapper>
        {
            Wrap("key1", "Primary-1"),
            Wrap("key8", "Reattempt-1"),
            Wrap("key13", "Reattempt-2"),
            Wrap("key2", "Primary-2")
        };
        strategy.Setup(x => x.BuildIndexerKeyRing(0)).Returns(ring);
        strategy.Setup(x => x.GetApplication(ApplicationUsage.Indexer)).Returns(ring[0]);
        var wrapper = CreateWrapper("key1", ring, initialRingIndex: 0, ApplicationUsage.Indexer, indexerKeyStateService: null);

        // Act
        var first = wrapper.CurrentApplication.ApiKey;
        var canRotateFromFirst = wrapper.CanRotate;
        wrapper.Rotate();
        var second = wrapper.CurrentApplication.ApiKey;
        var canRotateFromSecond = wrapper.CanRotate;
        wrapper.Rotate();
        var third = wrapper.CurrentApplication.ApiKey;
        var canRotateFromThird = wrapper.CanRotate;
        wrapper.Rotate();
        var fourth = wrapper.CurrentApplication.ApiKey;
        var canRotateFromFourth = wrapper.CanRotate;
        var act = () => wrapper.Rotate();

        // Assert
        first.Should().Be("key1");
        canRotateFromFirst.Should().BeTrue();
        second.Should().Be("key8");
        canRotateFromSecond.Should().BeTrue();
        third.Should().Be("key13");
        canRotateFromThird.Should().BeTrue();
        fourth.Should().Be("key2");
        canRotateFromFourth.Should().BeTrue();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "When the wrapper is created with a saved ring index, it starts at that position, because a restarted indexer must resume the same key.")]
    public void Create_WithInitialRingIndex_StartsAtSavedPosition()
    {
        // Arrange
        var ring = new List<ApplicationWrapper>
        {
            Wrap("key1", "Primary-1"),
            Wrap("key8", "Reattempt-1"),
            Wrap("key13", "Reattempt-2")
        };

        // Act
        var wrapper = CreateWrapper("key13", ring, initialRingIndex: 2, ApplicationUsage.Indexer, indexerKeyStateService: null);

        // Assert
        wrapper.CurrentApplication.ApiKey.Should().Be("key13");
        wrapper.IndexerRingIndex.Should().Be(2);
    }

    [Fact(DisplayName = "When DisposeAsync runs after an indexer rotate, the ring state is persisted, because the next run must resume at the next key.")]
    public async Task DisposeAsync_PersistsIndexerRingState()
    {
        // Arrange
        var ring = new List<ApplicationWrapper>
        {
            Wrap("key1", "Primary-1"),
            Wrap("key8", "Reattempt-1")
        };
        var stateService = _mocker.GetMock<IYouTubeIndexerKeyStateService>();
        var wrapper = CreateWrapper("key1", ring, initialRingIndex: 0, ApplicationUsage.Indexer, stateService.Object);
        wrapper.Rotate();

        // Act
        await wrapper.DisposeAsync();

        // Assert
        stateService.Verify(
            x => x.PersistSessionEndAsync(1, "key8", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "When a ServiceProvider disposes a CLI YouTube wrapper synchronously, Dispose does not throw, because Generic Host calls IDisposable.Dispose rather than DisposeAsync.")]
    public void Dispose_DoesNotThrowWhenServiceProviderDisposesSynchronously()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(CreateCliWrapper());
        var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<YouTubeServiceWrapper>();

        // Act
        var act = () => provider.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "When Dispose runs after an indexer rotate, the ring state is persisted, because sync shutdown must match DisposeAsync.")]
    public void Dispose_PersistsIndexerRingState()
    {
        // Arrange
        var ring = new List<ApplicationWrapper>
        {
            Wrap("key1", "Primary-1"),
            Wrap("key8", "Reattempt-1")
        };
        var stateService = _mocker.GetMock<IYouTubeIndexerKeyStateService>();
        var wrapper = CreateWrapper("key1", ring, initialRingIndex: 0, ApplicationUsage.Indexer, stateService.Object);
        wrapper.Rotate();

        // Act
        wrapper.Dispose();

        // Assert
        stateService.Verify(
            x => x.PersistSessionEndAsync(1, "key8", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private YouTubeServiceWrapper CreateCliWrapper()
    {
        var application = new ApplicationWrapper(
            new Application
            {
                ApiKey = "key1",
                Name = "CultPodcasts",
                Usage = ApplicationUsage.Cli,
                DisplayName = "CLI-1"
            }, 0, 0);

        return new YouTubeServiceWrapper(
            CreateService("key1"),
            application,
            ApplicationUsage.Cli,
            _mocker.GetMock<IYouTubeApiKeyStrategy>().Object,
            indexerKeyRing: null,
            initialRingIndex: 0,
            indexerKeyStateService: null,
            NullLogger<YouTubeServiceWrapper>.Instance);
    }

    private YouTubeServiceWrapper CreateWrapper(
        string apiKey,
        IReadOnlyList<ApplicationWrapper> ring,
        int initialRingIndex,
        ApplicationUsage usage,
        IYouTubeIndexerKeyStateService? indexerKeyStateService)
    {
        return new YouTubeServiceWrapper(
            CreateService(apiKey),
            ring[initialRingIndex],
            usage,
            _mocker.GetMock<IYouTubeApiKeyStrategy>().Object,
            ring,
            initialRingIndex,
            indexerKeyStateService,
            NullLogger<YouTubeServiceWrapper>.Instance);
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