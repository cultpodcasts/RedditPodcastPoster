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

public class DiscoverySubmissionHoldServiceTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<DiscoverySubmission> _saved = [];
    private readonly HashSet<Guid> _refuseTransition = [];
    private IReadOnlyList<DiscoveryResultsDocument> _documents = [];

    public DiscoverySubmissionHoldServiceTests()
    {
        _mocker.Use(TimeProvider.System);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDocuments(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(() => _documents);
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.TryTransitionState(It.IsAny<Guid>(), It.IsAny<DiscoveryResultsDocumentState>(),
                It.IsAny<DiscoveryResultsDocumentState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, DiscoveryResultsDocumentState _, DiscoveryResultsDocumentState _, CancellationToken _) =>
                !_refuseTransition.Contains(id));
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.Save(It.IsAny<DiscoverySubmission>(), It.IsAny<CancellationToken>()))
            .Callback((DiscoverySubmission s, CancellationToken _) => _saved.Add(s))
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "When a supervised curator submits unprocessed reports, a Pending submission is saved with the submitter and their accepted results, and every report moves to PendingSupervisor, because the work must wait for a supervisor.")]
    public async Task holds_submission_and_marks_reports_pending_supervisor()
    {
        // Arrange
        var report = CreateReport(3);
        _documents = [report];
        var accepted = report.DiscoveryResults.First().Id;
        var subject = _fixture.Create<string>();
        var sut = _mocker.CreateInstance<DiscoverySubmissionHoldService>();

        // Act
        var result = await sut.HoldAsync(Request([report.Id], accepted), subject, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionHoldStatus.Held);
        var saved = _saved.Should().ContainSingle().Subject;
        saved.Id.Should().Be(result.SubmissionId!.Value);
        saved.Status.Should().Be(DiscoverySubmissionStatus.Pending);
        saved.SubmitterSubject.Should().Be(subject);
        saved.DiscoveryResultsDocumentIds.Should().Equal(report.Id);
        saved.SubmitterAcceptedResultIds.Should().Equal(accepted);
        saved.SupervisorAcceptedResultIds.Should().BeNull();
        saved.ItemComments.Should().BeNull();
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(report.Id,
            DiscoveryResultsDocumentState.Unprocessed, DiscoveryResultsDocumentState.PendingSupervisor,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "INVARIANT: Holding a supervised submit ingests nothing (no URL submitter, no search indexing) and never marks reports Processed, because only Processed reports are ingested and that happens when a supervisor applies.")]
    public async Task hold_does_not_ingest()
    {
        // Arrange
        var report = CreateReport(2);
        _documents = [report];
        var sut = _mocker.CreateInstance<DiscoverySubmissionHoldService>();

        // Act
        await sut.HoldAsync(Request([report.Id], report.DiscoveryResults.Select(r => r.Id).ToArray()),
            _fixture.Create<string>(), CancellationToken.None);

        // Assert
        _mocker.GetMock<RedditPodcastPoster.UrlSubmission.Submitters.IDiscoveryUrlSubmitter>().VerifyNoOtherCalls();
        _mocker.GetMock<RedditPodcastPoster.EntitySearchIndexer.Services.IEpisodeSearchIndexerService>().VerifyNoOtherCalls();
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.MarkAsProcessed(
            It.IsAny<Guid[]>(), It.IsAny<Guid[]>(), It.IsAny<Guid[]>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(It.IsAny<Guid>(),
            It.IsAny<DiscoveryResultsDocumentState>(), DiscoveryResultsDocumentState.Processed,
            It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.SetProcessed(It.IsAny<IEnumerable<Guid>>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the submitter's accepted ids include results outside the submitted reports, the hold is refused naming them and nothing is held or saved, because the submitter must learn its input was wrong and the stored accepted set must be exactly what it sent.")]
    public async Task refuses_unknown_result_ids()
    {
        // Arrange
        var report = CreateReport(2);
        _documents = [report];
        var unknown = _fixture.Create<Guid>();
        var sut = _mocker.CreateInstance<DiscoverySubmissionHoldService>();

        // Act
        var result = await sut.HoldAsync(Request([report.Id], report.DiscoveryResults.First().Id, unknown),
            _fixture.Create<string>(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionHoldStatus.InvalidResultIds);
        result.UnknownResultIds.Should().Equal(unknown);
        _saved.Should().BeEmpty();
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(It.IsAny<Guid>(),
            It.IsAny<DiscoveryResultsDocumentState>(), It.IsAny<DiscoveryResultsDocumentState>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When saving the submission fails (possibly after committing), the submission is deleted and the reports are released, because a Pending submission must never point at reports that are curatable again.")]
    public async Task save_failure_deletes_submission_and_releases_reports()
    {
        // Arrange
        var report = CreateReport(1);
        _documents = [report];
        Guid? savedId = null;
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.Save(It.IsAny<DiscoverySubmission>(), It.IsAny<CancellationToken>()))
            .Callback((DiscoverySubmission s, CancellationToken _) => savedId = s.Id)
            .ThrowsAsync(new InvalidOperationException());
        var sut = _mocker.CreateInstance<DiscoverySubmissionHoldService>();

        // Act
        var result = await sut.HoldAsync(Request([report.Id]), _fixture.Create<string>(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionHoldStatus.Failed);
        _mocker.GetMock<IDiscoverySubmissionsRepository>().Verify(r => r.Delete(savedId!.Value, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(report.Id,
            DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Unprocessed,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When a submitted report is no longer Unprocessed, the hold is refused with a conflict naming it and nothing is saved, because it is already held or processed.")]
    public async Task refuses_report_not_unprocessed()
    {
        // Arrange
        var report = CreateReport(1);
        report.State = DiscoveryResultsDocumentState.PendingSupervisor;
        _documents = [report];
        var sut = _mocker.CreateInstance<DiscoverySubmissionHoldService>();

        // Act
        var result = await sut.HoldAsync(Request([report.Id]), _fixture.Create<string>(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionHoldStatus.Conflict);
        result.UnavailableDocumentIds.Should().Equal(report.Id);
        _saved.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When another caller changes a report mid-hold, reports already moved go back to Unprocessed and nothing is saved, because a partial hold would strand reports.")]
    public async Task concurrent_change_releases_moved_reports()
    {
        // Arrange
        var first = CreateReport(1);
        var second = CreateReport(1);
        _documents = [first, second];
        _refuseTransition.Add(second.Id);
        var sut = _mocker.CreateInstance<DiscoverySubmissionHoldService>();

        // Act
        var result = await sut.HoldAsync(Request([first.Id, second.Id]), _fixture.Create<string>(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionHoldStatus.Conflict);
        _saved.Should().BeEmpty();
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(first.Id,
            DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Unprocessed,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private DiscoveryResultsDocument CreateReport(int results) =>
        new(DateTime.UtcNow.AddDays(-1),
            Enumerable.Range(0, results).Select(_ => new DiscoveryResult { Id = _fixture.Create<Guid>() }).ToList())
        {
            SearchSince = _fixture.Create<string>()
        };

    private static DiscoverySubmitRequest Request(Guid[] documentIds, params Guid[] accepted) =>
        new() { DiscoveryResultsDocumentIds = documentIds, ResultIds = accepted };
}
