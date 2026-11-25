using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Services;
using Microsoft.DurableTask;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Twitter.Posters;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Characterises Tweet activity enable/disable gates and YouTube/Spotify availability flags before the
/// function-based Durable migration.
/// </summary>
public class TweetActivityGateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TweetActivityGateRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions()));
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(x => x.Start(It.IsAny<string>()))
            .Returns(_mocker.GetMock<IMemoryProbeScope>().Object);
        var enabledReason = "enabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunTweet(out enabledReason))
            .Returns(true);
        _mocker.GetMock<ITweeter>()
            .Setup(x => x.Tweet(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<TaskActivityContext>()
            .SetupGet(x => x.InstanceId)
            .Returns(_fixture.Create<string>());
    }

    private Task<IndexerContext> Run(IndexerContext indexerContext) =>
        _mocker.CreateInstance<Tweet>().RunAsync(
            _mocker.GetMock<TaskActivityContext>().Object,
            indexerContext);

    [Fact(DisplayName =
        "When RunTweet is false, Tweet returns Success and does not call ITweeter, because a disabled tweet pass " +
        "must no-op.")]
    public async Task disabled_tweet_does_not_call_tweeter()
    {
        // Arrange
        var disabledReason = "disabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunTweet(out disabledReason))
            .Returns(false);

        // Act
        var result = await Run(new IndexerContext());

        // Assert
        result.Success.Should().BeTrue();
        _mocker.GetMock<ITweeter>().Verify(
            x => x.Tweet(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<PodcastEpisode>?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When RunTweet is enabled, Tweet calls ITweeter with YouTube and Spotify availability derived from " +
        "IndexerContext Skip* and *Error flags.")]
    public async Task enabled_tweet_passes_derived_availability_flags()
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
        _mocker.GetMock<ITweeter>().Verify(
            x => x.Tweet(
                true,
                false,
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()),
            Times.Once);
    }
}
