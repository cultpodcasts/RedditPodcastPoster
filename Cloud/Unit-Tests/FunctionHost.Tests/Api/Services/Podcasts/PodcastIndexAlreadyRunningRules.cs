using Api.Models;
using Api.Services.Podcasts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

/// <summary>
/// Podcast on-demand Busy maps through the service the same way catalogue AlreadyRunning does (#1160).
/// </summary>
public class PodcastIndexAlreadyRunningRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PodcastIndexAlreadyRunningRules()
    {
        _mocker.Use(NullLogger<PodcastIndexService>.Instance);
        _mocker.Use(Options.Create(new IndexerOptions { ReleasedDaysAgo = 4 }));
    }

    [Fact(DisplayName =
        "When the indexer reports AlreadyRunning, the podcast index service returns AlreadyRunning and does not push " +
        "search, because the API must answer 409 indexingInProgress.")]
    public async Task already_running_maps_status_without_search()
    {
        // Arrange
        _mocker.GetMock<IIndexer>()
            .Setup(x => x.Index(It.IsAny<string>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new IndexResponse(IndexStatus.AlreadyRunning));
        var sut = _mocker.CreateInstance<PodcastIndexService>();

        // Act
        var result = await sut.IndexAsync(_fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastIndexStatus.AlreadyRunning);
        result.SearchIndexer.Should().BeNull();
    }
}
