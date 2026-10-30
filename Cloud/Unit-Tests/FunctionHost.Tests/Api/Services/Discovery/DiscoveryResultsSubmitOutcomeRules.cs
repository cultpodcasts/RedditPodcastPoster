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
        "MarkAsProcessed stamps AcceptError instead of Accepted when an accepted id has no outcome entry, so reconcile never reports success without navigable ids.")]
    public async Task mark_as_processed_missing_outcome_is_accept_error()
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

        // Act
        await sut.MarkAsProcessed([report.Id], [resultId], [], []);

        // Assert
        var stamped = report.DiscoveryResults.Single();
        stamped.State.Should().Be(DiscoveryResultState.AcceptError);
        stamped.AcceptedPodcastId.Should().BeNull();
        stamped.AcceptedEpisodeId.Should().BeNull();
        stamped.AcceptError.Should().Contain("Missing submit outcome");
    }

    [Fact(DisplayName =
        "UpdateResultStates restamps Accepted podcast and episode ids from retry outcomes so a lost retry response can still be reconciled.")]
    public async Task update_result_states_stamps_accepted_ids()
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
                State = DiscoveryResultState.AcceptError,
                AcceptError = _fixture.Create<string>()
            }
        ])
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
        await sut.UpdateResultStates([report.Id], [resultId], [], outcomes);

        // Assert
        var stamped = report.DiscoveryResults.Single();
        stamped.State.Should().Be(DiscoveryResultState.Accepted);
        stamped.AcceptedPodcastId.Should().Be(podcastId);
        stamped.AcceptedEpisodeId.Should().Be(episodeId);
        stamped.AcceptError.Should().BeNull();
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.Save(report), Times.Once);
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
        items[0].State.Should().Be(DiscoverySubmitOutcomeState.Accepted);
        items[0].PodcastId.Should().Be(podcastId);
        items[0].EpisodeId.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "GetSubmitOutcome returns Pending when the result is on a claimed report but State is still unset, so the client keeps polling after a lost Save.")]
    public async Task get_submit_outcome_pending_when_unstamped()
    {
        // Arrange
        var resultId = _fixture.Create<Guid>();
        var episodeName = _fixture.Create<string>();
        var showName = _fixture.Create<string>();
        var report = new DiscoveryResultsDocument(DateTime.UtcNow,
        [
            new DiscoveryResult { Id = resultId, EpisodeName = episodeName, ShowName = showName }
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
        items[0].State.Should().Be(DiscoverySubmitOutcomeState.Pending);
        items[0].EpisodeName.Should().Be(episodeName);
        items[0].ShowName.Should().Be(showName);
        items[0].PodcastId.Should().BeNull();
        items[0].EpisodeId.Should().BeNull();
    }

    [Fact(DisplayName =
        "GetSubmitOutcome returns Unknown when the result id is not on any of the given documents, so the client can distinguish a bad id from an in-flight stamp.")]
    public async Task get_submit_outcome_unknown_when_missing()
    {
        // Arrange
        var knownId = _fixture.Create<Guid>();
        var missingId = _fixture.Create<Guid>();
        var report = new DiscoveryResultsDocument(DateTime.UtcNow, [new DiscoveryResult { Id = knownId }])
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.Processed
        };
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<Guid>>()))
            .Returns(new[] { report }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        var items = await sut.GetSubmitOutcome([report.Id], [missingId]);

        // Assert
        items.Should().ContainSingle();
        items[0].DiscoveryItemId.Should().Be(missingId);
        items[0].State.Should().Be(DiscoverySubmitOutcomeState.Unknown);
    }

    [Fact(DisplayName =
        "GetSubmitOutcome returns Rejected for stamped rejected results so reconcile can show items that were not accepted.")]
    public async Task get_submit_outcome_rejected()
    {
        // Arrange
        var resultId = _fixture.Create<Guid>();
        var report = new DiscoveryResultsDocument(DateTime.UtcNow,
        [
            new DiscoveryResult
            {
                Id = resultId,
                State = DiscoveryResultState.Rejected,
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
        items[0].State.Should().Be(DiscoverySubmitOutcomeState.Rejected);
        items[0].PodcastId.Should().BeNull();
        items[0].EpisodeId.Should().BeNull();
    }
}
