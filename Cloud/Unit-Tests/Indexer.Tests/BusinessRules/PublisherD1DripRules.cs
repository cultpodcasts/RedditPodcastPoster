using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Services;
using Microsoft.DurableTask;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Search.Services;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class PublisherD1DripRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PublisherD1DripRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions()));
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(x => x.Start(It.IsAny<string>()))
            .Returns(_mocker.GetMock<IMemoryProbeScope>().Object);
        var reason = "enabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunPublisher(out reason))
            .Returns(true);
        _mocker.GetMock<ID1SearchChangeFeed>()
            .Setup(x => x.Drip(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new D1SearchChangeFeedResult(true, true, 0, 0, 0, false));
    }

    private Task<IndexerContext> Run() =>
        _mocker.CreateInstance<global::Indexer.Activities.Publisher>().RunAsync(
            _mocker.GetMock<TaskActivityContext>().Object,
            new IndexerContext(PublisherOperationId: _fixture.CreateGuid()));

    [Fact(DisplayName =
        "When the publisher activity runs, then the D1 search drip runs after the Azure indexer and the homepage, " +
        "because D1 is fed hourly from Cosmos next to the Azure pull indexers (SP-2.1).")]
    public async Task drip_runs_after_a_successful_publish()
    {
        // Arrange

        // Act
        var result = await Run();

        // Assert
        result.Success.Should().BeTrue();
        _mocker.GetMock<ID1SearchChangeFeed>().Verify(x => x.Drip(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When the Azure indexer run throws, then the D1 search drip still runs and the activity still fails, " +
        "because D1 must not depend on Azure being healthy.")]
    public async Task drip_runs_when_the_azure_indexer_fails()
    {
        // Arrange
        _mocker.GetMock<ISearchIndexerService>()
            .Setup(x => x.RunIndexer())
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));

        // Act
        Func<Task> act = Run;

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mocker.GetMock<ID1SearchChangeFeed>().Verify(x => x.Drip(It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IHomepagePublisher>().Verify(x => x.PublishHomepage(), Times.Never);
    }

    [Fact(DisplayName =
        "When the D1 search drip throws, then the publisher activity still succeeds, because a D1 failure must never " +
        "fail the hour or Azure indexing.")]
    public async Task a_failing_drip_never_fails_the_activity()
    {
        // Arrange
        _mocker.GetMock<ID1SearchChangeFeed>()
            .Setup(x => x.Drip(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));

        // Act
        var result = await Run();

        // Assert
        result.Success.Should().BeTrue();
    }
}
