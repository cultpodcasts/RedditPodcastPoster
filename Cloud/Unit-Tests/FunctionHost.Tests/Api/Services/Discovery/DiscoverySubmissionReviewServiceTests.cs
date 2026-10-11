using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoverySubmissionReviewServiceTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<DiscoverySubmission> _replaced = [];
    private readonly List<DiscoverySubmitRequest> _applied = [];
    private DiscoverySubmission? _submission;
    private IReadOnlyList<DiscoveryResultsDocument> _documents = [];
    private DiscoveryCurationSubmitResult _applyResult = new(DiscoveryCurationSubmitStatus.Failed);
    private bool _refuseReplace;

    public DiscoverySubmissionReviewServiceTests()
    {
        _mocker.Use(TimeProvider.System);
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _submission);
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.TryReplace(It.IsAny<DiscoverySubmission>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoverySubmission s, string _, CancellationToken _) =>
            {
                if (_refuseReplace)
                {
                    return null;
                }

                _replaced.Add(Snapshot(s));
                return _fixture.Create<string>();
            });
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDocuments(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(() => _documents);
        _mocker.GetMock<IDiscoveryCurationSubmitService>()
            .Setup(s => s.ApplyHeldAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()))
            .Callback((DiscoverySubmitRequest r, CancellationToken _) => _applied.Add(r))
            .ReturnsAsync(() => _applyResult);
    }

    [Fact(DisplayName =
        "When a supervisor applies a reworked set, the held reports are ingested with exactly the supervisor's final set, and the submission records Applied, the supervisor, appliedAt and the added/removed diff, because the supervisor's corrections are the outcome and teach the AI.")]
    public async Task apply_ingests_final_set_and_records_outcome()
    {
        // Arrange
        var (report, kept, dropped, missed) = ArrangeHeldSubmission();
        _applyResult = Ok();
        var supervisor = Supervisor();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest([kept, missed]), supervisor, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        var applied = _applied.Should().ContainSingle().Subject;
        applied.DiscoveryResultsDocumentIds.Should().Equal(report.Id);
        applied.ResultIds.Should().BeEquivalentTo([kept, missed]);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.Applied);
        final.SupervisorSubject.Should().Be(supervisor.Subject);
        final.AppliedAt.Should().NotBeNull();
        final.SupervisorAcceptedResultIds.Should().BeEquivalentTo([kept, missed]);
        final.AddedBySupervisorResultIds.Should().Equal(missed);
        final.RemovedBySupervisorResultIds.Should().Equal(dropped);
        final.History.Select(h => h.Action).Should().EndWith(
            [DiscoverySubmissionReviewService.ApplyStartedAction, DiscoverySubmissionReviewService.AppliedAction]);
    }

    [Fact(DisplayName =
        "INVARIANT: The apply claims the submission (ETag-guarded) before ingesting, so a concurrent apply that loses the race ingests nothing.")]
    public async Task concurrent_apply_ingests_nothing()
    {
        // Arrange
        ArrangeHeldSubmission();
        _refuseReplace = true;
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _applied.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the ingest fails, the submission becomes ApplyFailed with an error and is not marked Applied, so it stays in the queue and its reports stay PendingSupervisor for a retry.")]
    public async Task failed_apply_records_apply_failed()
    {
        // Arrange
        ArrangeHeldSubmission();
        _applyResult = new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Failed);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.ApplyFailed);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.ApplyFailed);
        final.ApplyError.Should().NotBeNullOrWhiteSpace();
        final.AppliedAt.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a held report is no longer PendingSupervisor at apply time, the apply is refused as a conflict and nothing is ingested, because the submission no longer owns that report.")]
    public async Task stale_report_refuses_apply()
    {
        // Arrange
        var (report, _, _, _) = ArrangeHeldSubmission();
        report.State = DiscoveryResultsDocumentState.Unprocessed;
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        result.InvalidIds.Should().Equal(report.Id);
        _applied.Should().BeEmpty();
        _replaced.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the caller's ETag is stale, the apply is refused as a conflict and nothing is ingested, because the supervisor must apply what they last saw.")]
    public async Task stale_etag_refuses_apply()
    {
        // Arrange
        ArrangeHeldSubmission();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id,
            new DiscoverySubmissionApplyRequest { ETag = _fixture.Create<string>() }, Supervisor(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _applied.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the final set includes a result outside the held reports, the apply is rejected as invalid and nothing is ingested, because a supervisor reworks only within the held reports.")]
    public async Task foreign_result_rejected()
    {
        // Arrange
        ArrangeHeldSubmission();
        var foreign = _fixture.Create<Guid>();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest([foreign]), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Invalid);
        result.InvalidIds.Should().Equal(foreign);
        _applied.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the submission names its supervisors, a non-mapped supervisor's apply is forbidden and nothing is ingested.")]
    public async Task unmapped_supervisor_apply_forbidden()
    {
        // Arrange
        ArrangeHeldSubmission();
        _submission!.SupervisorSubjects = [_fixture.Create<string>()];
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Forbidden);
        _applied.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an already-applied submission is applied again, the call succeeds as a no-op and ingests nothing, because apply is idempotent.")]
    public async Task reapply_is_noop()
    {
        // Arrange
        ArrangeHeldSubmission();
        _submission!.Status = DiscoverySubmissionStatus.Applied;
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Applied);
        _applied.Should().BeEmpty();
        _replaced.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When no final set is sent, apply uses the supervisor's saved review set, because a saved draft review is what the supervisor chose.")]
    public async Task apply_defaults_to_saved_review_set()
    {
        // Arrange
        var (_, _, _, missed) = ArrangeHeldSubmission();
        _submission!.SupervisorAcceptedResultIds = [missed];
        _applyResult = Ok();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        await sut.Apply(_submission.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        _applied.Should().ContainSingle().Which.ResultIds.Should().Equal(missed);
    }

    [Fact(DisplayName =
        "Saving a draft review stores the report comment, item comments and final set with its diff, keeps the submission Pending and ingests nothing, because only apply acts on the work.")]
    public async Task save_review_persists_comments_and_diff_without_ingest()
    {
        // Arrange
        var (_, kept, dropped, missed) = ArrangeHeldSubmission();
        var reportComment = _fixture.Create<string>();
        var itemComment = _fixture.Create<string>();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.SaveReview(_submission!.Id, new DiscoverySubmissionReviewRequest
        {
            ETag = _submission.ETag!,
            ReportComment = reportComment,
            ItemComments = [new DiscoverySubmissionItemCommentModel(dropped, itemComment)],
            AcceptedResultIds = [kept, missed]
        }, Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        result.ETag.Should().NotBeNullOrWhiteSpace();
        var saved = _replaced.Should().ContainSingle().Subject;
        saved.Status.Should().Be(DiscoverySubmissionStatus.Pending);
        saved.ReportComment.Should().Be(reportComment);
        saved.ItemComments.Should().ContainSingle().Which.Should()
            .BeEquivalentTo(new { ResultId = dropped, Comment = itemComment });
        saved.AddedBySupervisorResultIds.Should().Equal(missed);
        saved.RemovedBySupervisorResultIds.Should().Equal(dropped);
        _applied.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an item comment targets a result outside the held reports, the review save is rejected as invalid and nothing is stored.")]
    public async Task save_review_rejects_foreign_comment()
    {
        // Arrange
        ArrangeHeldSubmission();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.SaveReview(_submission!.Id, new DiscoverySubmissionReviewRequest
        {
            ETag = _submission.ETag!,
            ItemComments = [new DiscoverySubmissionItemCommentModel(_fixture.Create<Guid>(), _fixture.Create<string>())]
        }, Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Invalid);
        _replaced.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A supervisor's list is the queue of Pending and ApplyFailed submissions they may review, with a count, excluding submissions mapped to other supervisors.")]
    public async Task supervisor_list_is_reviewable_queue_with_count()
    {
        // Arrange
        var supervisor = Supervisor();
        var open = NewSubmission(DiscoverySubmissionStatus.Pending);
        var failed = NewSubmission(DiscoverySubmissionStatus.ApplyFailed);
        var other = NewSubmission(DiscoverySubmissionStatus.Pending);
        other.SupervisorSubjects = [_fixture.Create<string>()];
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetByStatus(DiscoverySubmissionStatus.Pending, It.IsAny<CancellationToken>()))
            .Returns(new[] { open, other }.ToAsyncEnumerable());
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetByStatus(DiscoverySubmissionStatus.ApplyFailed, It.IsAny<CancellationToken>()))
            .Returns(new[] { failed }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.List(supervisor, CancellationToken.None);

        // Assert
        result.Value!.IsSupervisorView.Should().BeTrue();
        result.Value.PendingCount.Should().Be(2);
        result.Value.Submissions.Select(x => x.Id).Should().BeEquivalentTo([open.Id, failed.Id]);
    }

    [Fact(DisplayName =
        "A supervised curator's list is their own submissions in any status, because they need to see outcomes and supervisor comments.")]
    public async Task submitter_list_is_own_submissions()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var applied = NewSubmission(DiscoverySubmissionStatus.Applied);
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetBySubmitter(subject, It.IsAny<CancellationToken>()))
            .Returns(new[] { applied }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.List(new SupervisionCaller(subject, false, false), CancellationToken.None);

        // Assert
        result.Value!.IsSupervisorView.Should().BeFalse();
        result.Value.Submissions.Should().ContainSingle().Which.Id.Should().Be(applied.Id);
        _mocker.GetMock<IDiscoverySubmissionsRepository>().Verify(
            r => r.GetByStatus(It.IsAny<DiscoverySubmissionStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A caller who is neither the submitter nor an allowed supervisor cannot read a submission.")]
    public async Task stranger_cannot_get_submission()
    {
        // Arrange
        ArrangeHeldSubmission();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Get(_submission!.Id, new SupervisionCaller(_fixture.Create<string>(), false, false),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionQueryStatus.Forbidden);
    }

    private (DiscoveryResultsDocument Report, Guid Kept, Guid Dropped, Guid Missed) ArrangeHeldSubmission()
    {
        var results = Enumerable.Range(0, 3).Select(_ => new DiscoveryResult { Id = _fixture.Create<Guid>() }).ToList();
        var report = new DiscoveryResultsDocument(DateTime.UtcNow.AddDays(-1), results)
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.PendingSupervisor
        };
        _documents = [report];
        _submission = NewSubmission(DiscoverySubmissionStatus.Pending);
        _submission.DiscoveryResultsDocumentIds = [report.Id];
        _submission.SubmitterAcceptedResultIds = [results[0].Id, results[1].Id];
        return (report, results[0].Id, results[1].Id, results[2].Id);
    }

    private DiscoverySubmission NewSubmission(DiscoverySubmissionStatus status) => new()
    {
        Id = _fixture.Create<Guid>(),
        Status = status,
        SubmitterSubject = _fixture.Create<string>(),
        SubmittedAt = DateTime.UtcNow.AddHours(-1),
        ETag = _fixture.Create<string>()
    };

    private DiscoverySubmissionApplyRequest ApplyRequest(Guid[]? accepted) =>
        new() { ETag = _submission!.ETag!, AcceptedResultIds = accepted };

    private SupervisionCaller Supervisor() => new(_fixture.Create<string>(), IsAdmin: false, IsSupervisor: true);

    private static DiscoveryCurationSubmitResult Ok() =>
        new(DiscoveryCurationSubmitStatus.Ok,
            new DiscoverySubmitOutcome("Success", false, [], new EntitySearchIndexerResponse()));

    private static DiscoverySubmission Snapshot(DiscoverySubmission s) =>
        System.Text.Json.JsonSerializer.Deserialize<DiscoverySubmission>(System.Text.Json.JsonSerializer.Serialize(s))!;
}
