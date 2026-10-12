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
    private readonly List<DiscoverySubmitRequest> _retried = [];
    private Func<DiscoverySubmitRequest, DiscoveryCurationSubmitResult> _ingest =
        _ => new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Failed);
    private bool _refuseReplace;
    private readonly HashSet<int> _refuseReplaceOnCall = [];
    private int _replaceCalls;
    private int _getCalls;
    private DiscoverySubmission? _rereadSubmission;
    private readonly string _failureReason;

    public DiscoverySubmissionReviewServiceTests()
    {
        _failureReason = _fixture.Create<string>();
        _mocker.Use(TimeProvider.System);
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++_getCalls > 1 && _rereadSubmission != null ? _rereadSubmission : _submission);
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.TryReplace(It.IsAny<DiscoverySubmission>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoverySubmission s, string _, CancellationToken _) =>
            {
                if (_refuseReplace || _refuseReplaceOnCall.Contains(++_replaceCalls))
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
            .ReturnsAsync((DiscoverySubmitRequest r, CancellationToken _) => _ingest(r));
        _mocker.GetMock<IDiscoveryCurationSubmitService>()
            .Setup(s => s.RetryItemsAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()))
            .Callback((DiscoverySubmitRequest r, CancellationToken _) => _retried.Add(r))
            .ReturnsAsync((DiscoverySubmitRequest r, CancellationToken _) => _ingest(r));
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetByStatus(It.IsAny<DiscoverySubmissionStatus>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<DiscoverySubmission>());
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.TryTransitionState(It.IsAny<Guid>(), It.IsAny<DiscoveryResultsDocumentState>(),
                It.IsAny<DiscoveryResultsDocumentState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact(DisplayName =
        "When a supervisor applies a reworked set, the held reports are ingested with exactly the supervisor's final set, and the submission records Applied, the supervisor, appliedAt and the added/removed diff, because the supervisor's corrections are the outcome and teach the AI.")]
    public async Task apply_ingests_final_set_and_records_outcome()
    {
        // Arrange
        var (report, kept, dropped, missed) = ArrangeHeldSubmission();
        _ingest = Ingest();
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
        _ingest = _ => new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Failed);
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
        _ingest = Ingest();
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
        "A supervisor's list is the queue of Pending, ApplyFailed and PartiallyApplied submissions they may review, with a count, excluding submissions mapped to other supervisors.")]
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

    [Fact(DisplayName =
        "When some results fail during apply, the submission becomes PartiallyApplied and records each result's outcome, with the failure reason on failed items, because a supervisor must see and deal with what failed.")]
    public async Task partial_failure_records_item_outcomes()
    {
        // Arrange
        var (_, kept, dropped, _) = ArrangeHeldSubmission();
        _ingest = Ingest(dropped);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest([kept, dropped]), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.PartiallyApplied);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.PartiallyApplied);
        final.ItemOutcomes.Should().ContainSingle(x => x.ResultId == kept).Which.State
            .Should().Be(DiscoverySubmissionItemState.Succeeded);
        var failed = final.ItemOutcomes!.Single(x => x.ResultId == dropped);
        failed.State.Should().Be(DiscoverySubmissionItemState.Failed);
        failed.Error.Should().Be(_failureReason);
        final.ErroredResultIds.Should().Equal(dropped);
    }

    [Fact(DisplayName =
        "When the apply run aborts part-way, results it never reached are recorded Failed with a reason, so they can be retried rather than silently lost.")]
    public async Task aborted_apply_marks_unreached_items_failed()
    {
        // Arrange
        var (_, kept, dropped, _) = ArrangeHeldSubmission();
        _ingest = r => new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Failed,
            new DiscoverySubmitOutcome("Failure", true,
                [new DiscoverySubmitItemOutcome(kept, _fixture.Create<Guid>(), _fixture.Create<Guid>(), "Created")],
                new EntitySearchIndexerResponse()));
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        await sut.Apply(_submission!.Id, ApplyRequest([kept, dropped]), Supervisor(), CancellationToken.None);

        // Assert
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.PartiallyApplied);
        final.ItemOutcomes!.Single(x => x.ResultId == dropped).Should().Match<DiscoverySubmissionItemOutcome>(x =>
            x.State == DiscoverySubmissionItemState.Failed && x.Error != null && x.Attempts == 0);
    }

    [Fact(DisplayName =
        "INVARIANT: Retry resubmits only the Failed items and never re-ingests Succeeded ones; when they all succeed the submission becomes Applied.")]
    public async Task retry_resubmits_only_failed()
    {
        // Arrange
        var (succeeded, failed) = ArrangePartiallyApplied();
        _ingest = Ingest();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Retry(_submission!.Id, RetryRequest(), Supervisor(), CancellationToken.None);

        // Assert
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Applied);
        _retried.Should().ContainSingle().Which.ResultIds.Should().Equal(failed);
        _retried.Single().ResultIds.Should().NotContain(succeeded);
        _applied.Should().BeEmpty();
        var final = _replaced.Last();
        final.ItemOutcomes!.Single(x => x.ResultId == failed).Should().Match<DiscoverySubmissionItemOutcome>(x =>
            x.State == DiscoverySubmissionItemState.Succeeded && x.Error == null && x.Attempts == 2);
        final.History.Select(h => h.Action).Should().Contain(DiscoverySubmissionReviewService.RetryStartedAction);
    }

    [Fact(DisplayName =
        "When a retried item fails again, the submission stays PartiallyApplied (still in the queue) with the new failure reason.")]
    public async Task retry_failing_again_stays_partially_applied()
    {
        // Arrange
        var (_, failed) = ArrangePartiallyApplied();
        _ingest = Ingest(failed);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Retry(_submission!.Id, RetryRequest(), Supervisor(), CancellationToken.None);

        // Assert
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.PartiallyApplied);
        _replaced.Last().ItemOutcomes!.Single(x => x.ResultId == failed).Error.Should().Be(_failureReason);
    }

    [Fact(DisplayName =
        "When the supervisor abandons every failed item, nothing is resubmitted and the submission closes as Applied with those items Abandoned.")]
    public async Task abandoning_failed_items_closes_submission()
    {
        // Arrange
        var (_, failed) = ArrangePartiallyApplied();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Retry(_submission!.Id, RetryRequest(failed), Supervisor(), CancellationToken.None);

        // Assert
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Applied);
        _retried.Should().BeEmpty();
        var final = _replaced.Last();
        final.ItemOutcomes!.Single(x => x.ResultId == failed).State.Should().Be(DiscoverySubmissionItemState.Abandoned);
        final.History.Select(h => h.Action).Should().Contain(DiscoverySubmissionReviewService.ItemsAbandonedAction);
    }

    [Fact(DisplayName =
        "A succeeded item cannot be abandoned or retried: the request is rejected as invalid and nothing is resubmitted.")]
    public async Task cannot_abandon_succeeded_item()
    {
        // Arrange
        var (succeeded, _) = ArrangePartiallyApplied();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Retry(_submission!.Id, RetryRequest(succeeded), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Invalid);
        result.InvalidIds.Should().Equal(succeeded);
        _retried.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Retrying a submission with no failed items (e.g. Applied) is refused as a conflict and nothing is resubmitted.")]
    public async Task cannot_retry_without_failed_items()
    {
        // Arrange
        ArrangePartiallyApplied();
        _submission!.Status = DiscoverySubmissionStatus.Applied;
        foreach (var item in _submission.ItemOutcomes!)
        {
            item.State = DiscoverySubmissionItemState.Succeeded;
        }

        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Retry(_submission.Id, RetryRequest(), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _retried.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A full apply is refused on a PartiallyApplied submission, because re-applying would re-ingest results that already succeeded.")]
    public async Task apply_refused_after_partial_apply()
    {
        // Arrange
        ArrangePartiallyApplied();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _applied.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the submission names its supervisors, a non-mapped supervisor's retry is forbidden and nothing is resubmitted.")]
    public async Task unmapped_supervisor_retry_forbidden()
    {
        // Arrange
        ArrangePartiallyApplied();
        _submission!.SupervisorSubjects = [_fixture.Create<string>()];
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Retry(_submission.Id, RetryRequest(), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Forbidden);
        _retried.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Releasing a Pending submission returns each held report PendingSupervisor to Unprocessed (guarded), marks the submission Released with the reason, and ingests nothing.")]
    public async Task release_returns_reports_and_ingests_nothing()
    {
        // Arrange
        var (report, _, _, _) = ArrangeHeldSubmission();
        var reason = _fixture.Create<string>();
        var supervisor = Supervisor();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Release(_submission!.Id,
            new DiscoverySubmissionReleaseRequest { ETag = _submission.ETag!, Reason = reason }, supervisor,
            CancellationToken.None);

        // Assert
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Released);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(report.Id,
            DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Unprocessed,
            It.IsAny<CancellationToken>()), Times.Once);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.Released);
        final.ReleaseReason.Should().Be(reason);
        final.ReleasedAt.Should().NotBeNull();
        final.History.Select(h => h.Action).Should().EndWith(DiscoverySubmissionReviewService.ReleasedAction);
        _applied.Should().BeEmpty();
        _retried.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A submission whose apply already ingested results cannot be released, because its reports are Processed and episodes exist.")]
    public async Task cannot_release_after_ingest()
    {
        // Arrange
        ArrangePartiallyApplied();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Release(_submission!.Id,
            new DiscoverySubmissionReleaseRequest { ETag = _submission.ETag! }, Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(It.IsAny<Guid>(),
            It.IsAny<DiscoveryResultsDocumentState>(), DiscoveryResultsDocumentState.Unprocessed,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Releasing with a stale ETag is refused as a conflict and no report is touched.")]
    public async Task release_with_stale_etag_refused()
    {
        // Arrange
        ArrangeHeldSubmission();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Release(_submission!.Id,
            new DiscoverySubmissionReleaseRequest { ETag = _fixture.Create<string>() }, Supervisor(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _replaced.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Only an admin may release a stuck report; a supervisor is forbidden and the report is untouched.")]
    public async Task stuck_report_release_requires_admin()
    {
        // Arrange
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.ReleaseStuckReport(_fixture.Create<Guid>(), new DiscoveryReportReleaseRequest(),
            Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Forbidden);
    }

    [Fact(DisplayName =
        "An admin cannot release a report still held by a live submission; the conflict names that submission, because releasing it must go through the submission.")]
    public async Task stuck_report_release_refused_when_live_submission_holds_it()
    {
        // Arrange
        var (report, _, _, _) = ArrangeHeldSubmission();
        _mocker.GetMock<IDiscoveryResultsRepository>().Setup(r => r.GetById(report.Id)).ReturnsAsync(report);
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetByStatus(DiscoverySubmissionStatus.Pending, It.IsAny<CancellationToken>()))
            .Returns(new[] { _submission! }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.ReleaseStuckReport(report.Id, new DiscoveryReportReleaseRequest(), Admin(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        result.InvalidIds.Should().Equal(_submission!.Id);
    }

    [Fact(DisplayName =
        "An admin can release a PendingSupervisor report that no live submission holds back to Unprocessed (guarded).")]
    public async Task stuck_report_released_by_admin()
    {
        // Arrange
        var (report, _, _, _) = ArrangeHeldSubmission();
        _mocker.GetMock<IDiscoveryResultsRepository>().Setup(r => r.GetById(report.Id)).ReturnsAsync(report);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.ReleaseStuckReport(report.Id, new DiscoveryReportReleaseRequest(), Admin(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(report.Id,
            DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Unprocessed,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "INVARIANT: The apply claim persists the submission as Applying (operation Apply) before ingesting, so review, release and retry cannot act on it mid-apply.")]
    public async Task apply_claim_persists_applying()
    {
        // Arrange
        ArrangeHeldSubmission();
        _ingest = Ingest();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        await sut.Apply(_submission!.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        var claim = _replaced.First();
        claim.Status.Should().Be(DiscoverySubmissionStatus.Applying);
        claim.InProgressOperation.Should().Be(DiscoverySubmissionOperation.Apply);
        claim.InProgressSince.Should().NotBeNull();
        var final = _replaced.Last();
        final.InProgressOperation.Should().BeNull();
        final.Status.Should().Be(DiscoverySubmissionStatus.Applied);
    }

    [Fact(DisplayName =
        "While a submission is Applying, release is refused and no report is touched, because the apply may already have ingested episodes.")]
    public async Task release_refused_while_applying()
    {
        // Arrange
        ArrangeApplying(DiscoverySubmissionOperation.Apply, DateTime.UtcNow);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Release(_submission!.Id,
            new DiscoverySubmissionReleaseRequest { ETag = _submission.ETag! }, Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _replaced.Should().BeEmpty();
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(It.IsAny<Guid>(),
            It.IsAny<DiscoveryResultsDocumentState>(), It.IsAny<DiscoveryResultsDocumentState>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "While a submission is Applying, saving a review or applying again is refused, because the in-progress operation owns it.")]
    public async Task review_and_apply_refused_while_applying()
    {
        // Arrange
        ArrangeApplying(DiscoverySubmissionOperation.Apply, DateTime.UtcNow);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var review = await sut.SaveReview(_submission!.Id,
            new DiscoverySubmissionReviewRequest { ETag = _submission.ETag!, ReportComment = _fixture.Create<string>() },
            Supervisor(), CancellationToken.None);
        var apply = await sut.Apply(_submission.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        review.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        apply.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _applied.Should().BeEmpty();
        _replaced.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An admin cannot release a report held by an Applying submission, because that submission is live.")]
    public async Task stuck_report_release_refused_when_applying_submission_holds_it()
    {
        // Arrange
        var report = ArrangeApplying(DiscoveryResultsDocumentState.PendingSupervisor);
        _mocker.GetMock<IDiscoveryResultsRepository>().Setup(r => r.GetById(report.Id)).ReturnsAsync(report);
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetByStatus(DiscoverySubmissionStatus.Applying, It.IsAny<CancellationToken>()))
            .Returns(new[] { _submission! }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.ReleaseStuckReport(report.Id, new DiscoveryReportReleaseRequest(), Admin(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        result.InvalidIds.Should().Equal(_submission!.Id);
    }

    [Fact(DisplayName =
        "INVARIANT: The retry claim persists Applying (operation Retry), so a concurrent retry with the fresh ETag is refused and resubmits nothing (no double ingest).")]
    public async Task concurrent_retry_refused()
    {
        // Arrange
        var (_, failed) = ArrangePartiallyApplied();
        _ingest = Ingest();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();
        await sut.Retry(_submission!.Id, RetryRequest(), Supervisor(), CancellationToken.None);
        var claim = _replaced.First();
        _submission = claim;
        _submission.ETag = _fixture.Create<string>();
        _retried.Clear();

        // Act
        var second = await sut.Retry(_submission.Id, RetryRequest(), Supervisor(), CancellationToken.None);

        // Assert
        claim.Status.Should().Be(DiscoverySubmissionStatus.Applying);
        claim.InProgressOperation.Should().Be(DiscoverySubmissionOperation.Retry);
        claim.InProgressResultIds.Should().Equal(failed);
        second.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _retried.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the final write gets a 412 but the stored submission is still Applying for this operation, the outcome is merged onto it and written, and the caller gets the real outcome.")]
    public async Task final_write_conflict_is_merged()
    {
        // Arrange
        ArrangeHeldSubmission();
        _ingest = Ingest();
        _refuseReplaceOnCall.Add(2);
        _rereadSubmission = AsApplyingCopy(DiscoverySubmissionOperation.Apply);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        result.ETag.Should().NotBeNullOrWhiteSpace();
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.Applied);
        final.History.Select(h => h.Action).Should().Contain(_rereadSubmission.History.Select(h => h.Action));
    }

    [Fact(DisplayName =
        "When the final write keeps failing (or the submission is no longer held by this operation), the caller gets an error, not Ok, because the outcome was never recorded.")]
    public async Task final_write_failure_is_reported()
    {
        // Arrange
        ArrangeHeldSubmission();
        _ingest = Ingest();
        _refuseReplaceOnCall.UnionWith([2, 3, 4, 5]);
        _rereadSubmission = AsApplyingCopy(DiscoverySubmissionOperation.Apply);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, ApplyRequest(null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.ApplyFailed);
        result.Message.Should().Be(DiscoverySubmissionReviewService.OutcomeNotRecorded);
        result.ETag.Should().BeNull();
    }

    [Fact(DisplayName =
        "An admin setting a stuck Applying submission's state records the new status, clears the in-progress fields and appends a StateChanged entry with from, to, actor and reason, because the override must be auditable.")]
    public async Task set_state_changes_status_and_records_history()
    {
        // Arrange
        ArrangeApplying(DiscoveryResultsDocumentState.Processed);
        var admin = Admin();
        var reason = _fixture.Create<string>();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.SetState(_submission!.Id, SetState("pending", reason), admin, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Pending);
        result.ETag.Should().NotBeNullOrWhiteSpace();
        var final = _replaced.Single();
        final.Status.Should().Be(DiscoverySubmissionStatus.Pending);
        final.InProgressOperation.Should().BeNull();
        final.InProgressSince.Should().BeNull();
        final.InProgressBy.Should().BeNull();
        final.InProgressResultIds.Should().BeNull();
        final.StatusBeforeInProgress.Should().BeNull();
        var entry = final.History.Last();
        entry.Action.Should().Be(DiscoverySubmissionReviewService.StateChangedAction);
        entry.ActorSubject.Should().Be(admin.Subject);
        entry.FromStatus.Should().Be(DiscoverySubmissionStatus.Applying);
        entry.ToStatus.Should().Be(DiscoverySubmissionStatus.Pending);
        entry.Reason.Should().Be(reason);
    }

    [Fact(DisplayName =
        "Setting the state never touches report states, item outcomes or ingestion, because the admin reconciles those separately.")]
    public async Task set_state_changes_nothing_else()
    {
        // Arrange
        ArrangeApplying(DiscoveryResultsDocumentState.Processed);
        var outcomes = new[]
        {
            new DiscoverySubmissionItemOutcome { ResultId = _fixture.Create<Guid>(), State = DiscoverySubmissionItemState.Failed }
        };
        _submission!.ItemOutcomes = outcomes;
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        await sut.SetState(_submission.Id, SetState("ApplyFailed", null), Admin(), CancellationToken.None);

        // Assert
        _mocker.GetMock<IDiscoveryResultsRepository>().Verify(r => r.TryTransitionState(It.IsAny<Guid>(),
            It.IsAny<DiscoveryResultsDocumentState>(), It.IsAny<DiscoveryResultsDocumentState>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _applied.Should().BeEmpty();
        _retried.Should().BeEmpty();
        var final = _replaced.Single();
        final.ItemOutcomes.Should().BeEquivalentTo(outcomes);
        final.History.Last().Reason.Should().BeNull();
    }

    [Theory(DisplayName =
        "Setting the state to an unknown name, None, a number or blank is invalid (400) and writes nothing.")]
    [InlineData("Bogus")]
    [InlineData("None")]
    [InlineData("1")]
    [InlineData("")]
    public async Task set_state_rejects_invalid_status(string status)
    {
        // Arrange
        ArrangeApplying(DiscoveryResultsDocumentState.Processed);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.SetState(_submission!.Id, SetState(status, null), Admin(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Invalid);
        _replaced.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Setting the state with a stale ETag is a conflict (409) and writes nothing.")]
    public async Task set_state_rejects_stale_etag()
    {
        // Arrange
        ArrangeApplying(DiscoveryResultsDocumentState.Processed);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.SetState(_submission!.Id,
            new DiscoverySubmissionSetStateRequest { Status = "Pending", ETag = _fixture.Create<string>() },
            Admin(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _replaced.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Setting the state is admin-only: a supervisor is forbidden.")]
    public async Task set_state_requires_admin()
    {
        // Arrange
        ArrangeApplying(DiscoveryResultsDocumentState.Processed);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.SetState(_submission!.Id, SetState("Pending", null), Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Forbidden);
        _replaced.Should().BeEmpty();
    }

    private DiscoverySubmissionSetStateRequest SetState(string status, string? reason) =>
        new() { Status = status, ETag = _submission!.ETag!, Reason = reason };

    private DiscoveryResultsDocument ArrangeApplying(DiscoveryResultsDocumentState reportState)
    {
        var (report, kept, dropped, _) = ArrangeHeldSubmission();
        report.State = reportState;
        ArrangeApplying(DiscoverySubmissionOperation.Apply,
            DateTime.UtcNow.AddHours(-1));
        _submission!.InProgressResultIds = [kept, dropped];
        return report;
    }

    private void ArrangeApplying(DiscoverySubmissionOperation operation, DateTime since)
    {
        if (_submission == null)
        {
            ArrangeHeldSubmission();
        }

        _submission!.StatusBeforeInProgress = _submission.Status;
        _submission.Status = DiscoverySubmissionStatus.Applying;
        _submission.InProgressOperation = operation;
        _submission.InProgressSince = since;
    }

    private DiscoverySubmission AsApplyingCopy(DiscoverySubmissionOperation operation)
    {
        var copy = Snapshot(_submission!);
        copy.Status = DiscoverySubmissionStatus.Applying;
        copy.InProgressOperation = operation;
        copy.ETag = _fixture.Create<string>();
        copy.History = [.. copy.History, new DiscoverySubmissionHistoryEntry
        {
            At = DateTime.UtcNow, ActorSubject = _fixture.Create<string>(), Action = _fixture.Create<string>()
        }];
        return copy;
    }

    private SupervisionCaller Admin() => new(_fixture.Create<string>(), IsAdmin: true, IsSupervisor: false);

    private (Guid Succeeded, Guid Failed) ArrangePartiallyApplied()
    {
        var (report, kept, dropped, _) = ArrangeHeldSubmission();
        report.State = DiscoveryResultsDocumentState.Processed;
        _submission!.Status = DiscoverySubmissionStatus.PartiallyApplied;
        _submission.ItemOutcomes =
        [
            new DiscoverySubmissionItemOutcome { ResultId = kept, State = DiscoverySubmissionItemState.Succeeded, Attempts = 1 },
            new DiscoverySubmissionItemOutcome
            {
                ResultId = dropped, State = DiscoverySubmissionItemState.Failed, Attempts = 1,
                Error = _fixture.Create<string>()
            }
        ];
        return (kept, dropped);
    }

    private DiscoverySubmissionRetryRequest RetryRequest(params Guid[] abandon) =>
        new() { ETag = _submission!.ETag!, AbandonResultIds = abandon.Length > 0 ? abandon : null };

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

    /// <summary>Ingest double: every requested result succeeds except <paramref name="failing"/>, which fail with a reason.</summary>
    private Func<DiscoverySubmitRequest, DiscoveryCurationSubmitResult> Ingest(params Guid[] failing) => r =>
        new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok,
            new DiscoverySubmitOutcome("Success", failing.Length > 0,
                r.ResultIds.Select(id => failing.Contains(id)
                    ? new DiscoverySubmitItemOutcome(id, null, null, DiscoverySubmitItemOutcomeMessages.Error, _failureReason)
                    : new DiscoverySubmitItemOutcome(id, _fixture.Create<Guid>(), _fixture.Create<Guid>(), "Created"))
                    .ToArray(),
                new EntitySearchIndexerResponse()));



    private static DiscoverySubmission Snapshot(DiscoverySubmission s) =>
        System.Text.Json.JsonSerializer.Deserialize<DiscoverySubmission>(System.Text.Json.JsonSerializer.Serialize(s))!;
}
