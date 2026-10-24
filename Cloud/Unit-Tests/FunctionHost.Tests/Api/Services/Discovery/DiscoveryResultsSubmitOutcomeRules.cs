using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryResultsSubmitOutcomeRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "MarkAsProcessed stamps Accepted results with podcast and episode ids from ingest outcomes so a lost Save response can still show what landed.")]
    public async Task mark_as_processed_stamps_accepted_ids()
    {
        // Arrange
        var resultId = _fixture.Create<Guid>();
        var podcastId = _fixture.Create<Guid>();
        var episodeId = _fixture.Create<Guid>();
        var report = new DiscoveryResultsDocument(DateTime.UtcNow, [new DiscoveryResult { Id = resultId }])
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.Processed
        };
        _mocker.GetMock<IDiscoveryResultsRepository>().Setup(r => r.GetById(report.Id)).ReturnsAsync(report);
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();
        var outcomes = new[]
        {
            new DiscoverySubmitItemOutcome(resultId, episodeId, podcastId, "Created")
        };

        // Act
        await sut.MarkAsProcessed([report.Id], [resultId], [], outcomes);

        // Assert
        report.DiscoveryResults.Single().State.Should().Be(DiscoveryResultState.Accepted);
        report.DiscoveryResults.Single().AcceptedPodcastId.Should().Be(podcastId);
        report.DiscoveryResults.Single().AcceptedEpisodeId.Should().Be(episodeId);
        report.DiscoveryResults.Single().AcceptError.Should().BeNull();
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.Save(report), Times.Once);
    }

    [Fact(DisplayName =
        "MarkAsProcessed stamps AcceptError with the outcome error text so reconcile can show why an item failed.")]
    public async Task mark_as_processed_stamps_accept_error()
    {
        // Arrange
        var resultId = _fixture.Create<Guid>();
        var report = new DiscoveryResultsDocument(DateTime.UtcNow, [new DiscoveryResult { Id = resultId }])
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.Processed
        };
        _mocker.GetMock<IDiscoveryResultsRepository>().Setup(r => r.GetById(report.Id)).ReturnsAsync(report);
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();
        var outcomes = new[]
        {
            new DiscoverySubmitItemOutcome(resultId, null, null, DiscoverySubmitItemOutcomeMessages.Error,
                "SpotifyAPI.Web.APIException")
        };

        // Act
        await sut.MarkAsProcessed([report.Id], [], [resultId], outcomes);

        // Assert
        var stamped = report.DiscoveryResults.Single();
        stamped.State.Should().Be(DiscoveryResultState.AcceptError);
        stamped.AcceptError.Should().Be("SpotifyAPI.Web.APIException");
    }

    [Fact(DisplayName =
        "GetSubmitOutcome returns Accepted with episode ids for stamped results, including Processed reports no longer in the live queue.")]
    public async Task get_submit_outcome_reads_stamps()
    {
        // Arrange
        var resultId = _fixture.Create<Guid>();
        var podcastId = _fixture.Create<Guid>();
        var episodeId = _fixture.Create<Guid>();
        var report = new DiscoveryResultsDocument(DateTime.UtcNow,
        [
            new DiscoveryResult
            {
                Id = resultId,
                State = DiscoveryResultState.Accepted,
                AcceptedPodcastId = podcastId,
                AcceptedEpisodeId = episodeId,
                EpisodeName = _fixture.Create<string>(),
                ShowName = _fixture.Create<string>()
            }
        ])
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.Processed
        };
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<Guid>>()))
            .Returns(new[] { report }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        var items = await sut.GetSubmitOutcome([report.Id], [resultId]);

        // Assert
        items.Should().ContainSingle();
        items[0].State.Should().Be("Accepted");
        items[0].PodcastId.Should().Be(podcastId);
        items[0].EpisodeId.Should().Be(episodeId);
    }
}
