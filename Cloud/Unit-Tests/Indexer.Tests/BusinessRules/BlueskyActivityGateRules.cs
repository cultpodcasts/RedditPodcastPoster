using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Characterises Bluesky activity enable/disable gates and YouTube/Spotify availability flags before the
/// function-based Durable migration.
/// </summary>
public class BlueskyActivityGateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public BlueskyActivityGateRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions()));
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(x => x.Start(It.IsAny<string>()))
            .Returns(_mocker.GetMock<IMemoryProbeScope>().Object);
        var enabledReason = "enabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunBluesky(out enabledReason))
            .Returns(true);
        _mocker.GetMock<IBlueskyPostManager>()
            .Setup(x => x.Post(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<FunctionContext>()
            .SetupGet(x => x.InvocationId)
            .Returns(_fixture.Create<string>());
    }

    private Task<IndexerContext> Run(IndexerContext indexerContext) =>
        _mocker.CreateInstance<Bluesky>().Run(
            indexerContext,
            _mocker.GetMock<FunctionContext>().Object);

    [Fact(DisplayName =
        "When RunBluesky is false, Bluesky returns Success and does not call IBlueskyPostManager, because a disabled " +
        "Bluesky pass must no-op.")]
    public async Task disabled_bluesky_does_not_call_post_manager()
    {
        // Arrange
        var disabledReason = "disabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunBluesky(out disabledReason))
            .Returns(false);

        // Act
        var result = await Run(new IndexerContext());

        // Assert
        result.Success.Should().BeTrue();
        _mocker.GetMock<IBlueskyPostManager>().Verify(
            x => x.Post(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<PodcastEpisode>?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When RunBluesky is enabled, Bluesky calls IBlueskyPostManager with YouTube and Spotify availability derived " +
        "from IndexerContext Skip* and *Error flags.")]
    public async Task enabled_bluesky_passes_derived_availability_flags()
    {
        // Arrange
        var indexerContext = new IndexerContext(
            SkipYouTubeUrlResolving: false,
            YouTubeError: false,
            SkipSpotifyUrlResolving: true,
            SpotifyError: true);

        // Act
        var result = await Run(indexerContext);

        // Assert
        result.Success.Should().BeTrue();
        _mocker.GetMock<IBlueskyPostManager>().Verify(
            x => x.Post(
                true,
                false,
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()),
            Times.Once);
    }
}
