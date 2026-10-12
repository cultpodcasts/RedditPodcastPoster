using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Services.Discovery;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryResultsServiceClaimTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly HashSet<Guid> _refuse = [];

    public DiscoveryResultsServiceClaimTests()
    {
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.TryTransitionState(It.IsAny<Guid>(), It.IsAny<DiscoveryResultsDocumentState>(),
                It.IsAny<DiscoveryResultsDocumentState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, DiscoveryResultsDocumentState _, DiscoveryResultsDocumentState _, CancellationToken _) =>
                !_refuse.Contains(id));
    }

    [Fact(DisplayName =
        "Claiming reports moves each Unprocessed report to Processed with a guarded transition, because the claim must win or lose atomically per report.")]
    public async Task claims_all_reports()
    {
        // Arrange
        var ids = new[] { _fixture.Create<Guid>(), _fixture.Create<Guid>() };
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        var unclaimed = await sut.TryClaimForProcessing(ids);

        // Assert
        unclaimed.Should().BeEmpty();
        foreach (var id in ids)
        {
            _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(id,
                DiscoveryResultsDocumentState.Unprocessed, DiscoveryResultsDocumentState.Processed,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact(DisplayName =
        "When one report cannot be claimed, reports already claimed are released back to Unprocessed and the unclaimable id is returned, because a submit is all-or-nothing.")]
    public async Task partial_claim_is_released()
    {
        // Arrange
        var first = _fixture.Create<Guid>();
        var second = _fixture.Create<Guid>();
        _refuse.Add(second);
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        var unclaimed = await sut.TryClaimForProcessing([first, second]);

        // Assert
        unclaimed.Should().Equal(second);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(first,
            DiscoveryResultsDocumentState.Processed, DiscoveryResultsDocumentState.Unprocessed,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "INVARIANT: Stamping result states never touches a report awaiting supervisor review, because only a supervisor may promote it.")]
    public async Task mark_as_processed_skips_pending_supervisor()
    {
        // Arrange
        var report = new DiscoveryResultsDocument(DateTime.UtcNow, [new DiscoveryResult { Id = _fixture.Create<Guid>() }])
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.PendingSupervisor
        };
        _mocker.GetMock<IDiscoveryResultsRepository>().Setup(r => r.GetById(report.Id)).ReturnsAsync(report);
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        await sut.MarkAsProcessed([report.Id], [], []);

        // Assert
        report.State.Should().Be(DiscoveryResultsDocumentState.PendingSupervisor);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.Save(It.IsAny<DiscoveryResultsDocument>()), Times.Never);
    }

    [Fact(DisplayName =
        "Releasing a claim moves each report Processed back to Unprocessed with a guarded transition, so a concurrently re-claimed report is not disturbed.")]
    public async Task release_claim_reverts_reports()
    {
        // Arrange
        var id = _fixture.Create<Guid>();
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        await sut.ReleaseClaim([id]);

        // Assert
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(id,
            DiscoveryResultsDocumentState.Processed, DiscoveryResultsDocumentState.Unprocessed,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Claiming held reports for a supervisor apply moves each PendingSupervisor report to Processed with a guarded transition before any ingest, because only a supervisor apply may process a held report.")]
    public async Task claims_held_reports_from_pending_supervisor()
    {
        // Arrange
        var id = _fixture.Create<Guid>();
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        var unclaimed = await sut.TryClaimHeldForProcessing([id]);

        // Assert
        unclaimed.Should().BeEmpty();
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(id,
            DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Processed,
            It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(It.IsAny<Guid>(),
            DiscoveryResultsDocumentState.Unprocessed, It.IsAny<DiscoveryResultsDocumentState>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When one held report cannot be claimed, the held reports already claimed go back to PendingSupervisor and the failed id is returned, because a partial apply would strand reports.")]
    public async Task failed_held_claim_returns_claimed_to_pending_supervisor()
    {
        // Arrange
        var first = _fixture.Create<Guid>();
        var second = _fixture.Create<Guid>();
        _refuse.Add(second);
        var sut = _mocker.CreateInstance<DiscoveryResultsService>();

        // Act
        var unclaimed = await sut.TryClaimHeldForProcessing([first, second]);

        // Assert
        unclaimed.Should().Equal(second);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(first,
            DiscoveryResultsDocumentState.Processed, DiscoveryResultsDocumentState.PendingSupervisor,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
