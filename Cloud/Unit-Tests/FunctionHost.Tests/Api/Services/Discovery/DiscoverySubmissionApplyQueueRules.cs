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

/// <summary>
/// #1415: discovery apply / retry no longer runs inside the HTTP request (14 Nov 52c0c38f and 21 Nov 872299ca both
/// outlived the ~125 s proxy cut-off while the backend finished). The request claims, preflights and queues; the queue
/// worker ingests and records the outcome.
/// </summary>
public class DiscoverySubmissionApplyQueueRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<DiscoverySubmission> _replaced = [];
    private readonly List<DiscoverySubmissionApplyQueueMessage> _queued = [];
    private readonly List<DiscoverySubmitRequest> _ingested = [];
    private DiscoverySubmission? _submission;
    private IReadOnlyList<DiscoveryResultsDocument> _documents = [];
    private DiscoveryCurationSubmitResult? _preflight;
    private bool _queueFails;

    public DiscoverySubmissionApplyQueueRules()
    {
        _mocker.Use(TimeProvider.System);
        var repository = _mocker.GetMock<IDiscoverySubmissionsRepository>();
        repository.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _submission);
        repository.Setup(r => r.TryReplace(It.IsAny<DiscoverySubmission>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoverySubmission s, string _, CancellationToken _) =>
            {
                _replaced.Add(Snapshot(s));
                s.ETag = _fixture.Create<string>();
                return s.ETag;
            });
        repository.Setup(r => r.GetByStatus(It.IsAny<DiscoverySubmissionStatus>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<DiscoverySubmission>());
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDocuments(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(() => _documents);
        var curation = _mocker.GetMock<IDiscoveryCurationSubmitService>();
        curation.Setup(s => s.PreflightAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _preflight);
        curation.Setup(s => s.ApplyHeldAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoverySubmitRequest r, CancellationToken _) => Created(r));
        curation.Setup(s => s.RetryItemsAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoverySubmitRequest r, CancellationToken _) => Created(r));
        _mocker.GetMock<IDiscoverySubmissionApplyQueue>()
            .Setup(q => q.EnqueueAsync(It.IsAny<DiscoverySubmissionApplyQueueMessage>(), It.IsAny<CancellationToken>()))
            .Returns((DiscoverySubmissionApplyQueueMessage m, CancellationToken _) =>
            {
                if (_queueFails)
                {
                    throw new InvalidOperationException("queue down");
                }

                _queued.Add(m);
                return Task.CompletedTask;
            });
    }

    [Fact(DisplayName =
        "INVARIANT (#1415): A clear apply answers Accepted straight after the claim: the submission is persisted Applying, one message carrying the claim and the decisions is queued, and nothing is ingested on the request.")]
    public async Task apply_queues_and_returns_accepted()
    {
        // Arrange
        var ids = ArrangeHeld(3);
        var decisions = new Dictionary<Guid, string> { [ids[0]] = "add" };
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id,
            new DiscoverySubmissionApplyRequest { ETag = _submission.ETag!, LanguageDecisions = decisions },
            Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Accepted);
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Applying);
        result.ETag.Should().Be(_submission.ETag, "the claimed etag, so the page can poll from it");
        _replaced.Should().ContainSingle().Which.Status.Should().Be(DiscoverySubmissionStatus.Applying);
        var message = _queued.Should().ContainSingle().Subject;
        message.SubmissionId.Should().Be(_submission.Id);
        message.Operation.Should().Be(DiscoverySubmissionOperation.Apply);
        message.InProgressSince.Should().Be(_submission.InProgressSince!.Value);
        message.LanguageDecisions.Should().BeEquivalentTo(decisions);
        _ingested.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the message cannot be queued, the claim is undone (the status before it is restored with the reason) and the caller gets an error, because nothing will ever run it.")]
    public async Task queue_failure_restores_status()
    {
        // Arrange
        ArrangeHeld(2);
        _queueFails = true;
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, new DiscoverySubmissionApplyRequest { ETag = _submission.ETag! },
            Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.ApplyFailed);
        result.Message.Should().Be(DiscoverySubmissionReviewService.QueueFailedMessage);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.Pending);
        final.InProgressOperation.Should().BeNull();
        final.ApplyError.Should().Be(DiscoverySubmissionReviewService.QueueFailedMessage);
        final.History.Select(h => h.Action).Should().EndWith(DiscoverySubmissionReviewService.ApplyQueueFailedAction);
        _ingested.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "The queue worker ingests the claimed results with the queued decisions, records Applied, clears the lock and flags the created episodes for the supervisor's review.")]
    public async Task worker_ingests_and_records_outcome()
    {
        // Arrange
        var ids = ArrangeHeld(2);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();
        await sut.Apply(_submission!.Id, new DiscoverySubmissionApplyRequest { ETag = _submission.ETag! }, Supervisor(),
            CancellationToken.None);

        // Act
        var result = await sut.ProcessQueued(_queued.Single(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        _ingested.Should().ContainSingle().Which.ResultIds.Should().BeEquivalentTo(ids);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.Applied);
        final.InProgressOperation.Should().BeNull();
        final.InProgressWorkerStartedAt.Should().BeNull();
        _replaced.Should().Contain(x => x.Status == DiscoverySubmissionStatus.Applying && x.InProgressWorkerStartedAt != null,
            "the worker records that it started before it ingests, so a redelivery knows");
    }

    [Fact(DisplayName =
        "A message for a claim that no longer holds the submission (already finished, or an admin changed its state) does nothing, so a redelivered message never ingests twice.")]
    public async Task stale_message_is_a_no_op()
    {
        // Arrange
        ArrangeHeld(2);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();
        await sut.Apply(_submission!.Id, new DiscoverySubmissionApplyRequest { ETag = _submission.ETag! }, Supervisor(),
            CancellationToken.None);
        await sut.ProcessQueued(_queued.Single(), CancellationToken.None);
        var writes = _replaced.Count;

        // Act
        var again = await sut.ProcessQueued(_queued.Single(), CancellationToken.None);

        // Assert
        again.Status.Should().Be(DiscoverySubmissionCommandStatus.Conflict);
        _ingested.Should().ContainSingle();
        _replaced.Should().HaveCount(writes);
    }

    [Fact(DisplayName =
        "A redelivery after the worker started, with the reports already claimed, does not ingest again: every unfinished item becomes Failed with the reason and the submission is retryable, never stuck Applying.")]
    public async Task redelivery_after_claim_ends_retryable()
    {
        // Arrange
        var ids = ArrangeHeld(2);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();
        await sut.Apply(_submission!.Id, new DiscoverySubmissionApplyRequest { ETag = _submission.ETag! }, Supervisor(),
            CancellationToken.None);
        _submission.InProgressWorkerStartedAt = DateTime.UtcNow.AddMinutes(-6);
        _documents[0].State = DiscoveryResultsDocumentState.Processed;

        // Act
        var result = await sut.ProcessQueued(_queued.Single(), CancellationToken.None);

        // Assert
        _ingested.Should().BeEmpty();
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.ApplyFailed);
        var final = _replaced.Last();
        final.InProgressOperation.Should().BeNull();
        final.ItemOutcomes!.Select(x => x.ResultId).Should().BeEquivalentTo(ids);
        final.ItemOutcomes.Should().OnlyContain(x => x.State == DiscoverySubmissionItemState.Failed && x.Error != null);
        DiscoverySubmissionRules.IsRetryable(final).Should().BeTrue();
        final.History.Select(h => h.Action).Should().Contain(DiscoverySubmissionReviewService.ApplyInterruptedAction);
    }

    [Fact(DisplayName =
        "A redelivered apply whose reports were never claimed created nothing, so it simply runs again.")]
    public async Task redelivery_before_claim_runs_again()
    {
        // Arrange
        ArrangeHeld(2);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();
        await sut.Apply(_submission!.Id, new DiscoverySubmissionApplyRequest { ETag = _submission.ETag! }, Supervisor(),
            CancellationToken.None);
        _submission.InProgressWorkerStartedAt = DateTime.UtcNow.AddMinutes(-6);

        // Act
        var result = await sut.ProcessQueued(_queued.Single(), CancellationToken.None);

        // Assert
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Applied);
        _ingested.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A poisoned apply whose reports were never claimed ends ApplyFailed with no item outcomes, so the supervisor can simply apply again instead of an admin unsticking Applying.")]
    public async Task poisoned_apply_before_claim_is_reappliable()
    {
        // Arrange
        ArrangeHeld(2);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();
        await sut.Apply(_submission!.Id, new DiscoverySubmissionApplyRequest { ETag = _submission.ETag! }, Supervisor(),
            CancellationToken.None);

        // Act
        await sut.AbandonPoisoned(_queued.Single(), CancellationToken.None);

        // Assert
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.ApplyFailed);
        final.ItemOutcomes.Should().BeNull();
        final.InProgressOperation.Should().BeNull();
        DiscoverySubmissionRules.IsReviewable(final).Should().BeTrue();
        _ingested.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A decision hold is still answered on the request (nothing queued), so the page can prompt and re-POST as before.")]
    public async Task preflight_hold_is_not_queued()
    {
        // Arrange
        ArrangeHeld(1);
        _preflight = new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.IdentityDecisionRequired,
            new DiscoverySubmitOutcome("IdentityDecisionRequired", false, [], new EntitySearchIndexerResponse()));
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id, new DiscoverySubmissionApplyRequest { ETag = _submission.ETag! },
            Supervisor(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.IdentityDecisionRequired);
        _queued.Should().BeEmpty();
        _replaced.Last().Status.Should().Be(DiscoverySubmissionStatus.Pending);
    }

    private DiscoveryCurationSubmitResult Created(DiscoverySubmitRequest request)
    {
        _ingested.Add(request);
        return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok,
            new DiscoverySubmitOutcome("Success", false,
                request.ResultIds.Select(id =>
                    new DiscoverySubmitItemOutcome(id, _fixture.Create<Guid>(), _fixture.Create<Guid>(), "Created")).ToArray(),
                new EntitySearchIndexerResponse()));
    }

    private Guid[] ArrangeHeld(int count)
    {
        var results = Enumerable.Range(0, count).Select(_ => new DiscoveryResult { Id = _fixture.Create<Guid>() }).ToList();
        var report = new DiscoveryResultsDocument(DateTime.UtcNow.AddDays(-1), results)
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.PendingSupervisor
        };
        _documents = [report];
        _submission = new DiscoverySubmission
        {
            Id = _fixture.Create<Guid>(),
            Status = DiscoverySubmissionStatus.Pending,
            SubmitterSubject = _fixture.Create<string>(),
            SubmittedAt = DateTime.UtcNow.AddHours(-1),
            ETag = _fixture.Create<string>(),
            DiscoveryResultsDocumentIds = [report.Id],
            SubmitterAcceptedResultIds = results.Select(x => x.Id).ToArray()
        };
        return results.Select(x => x.Id).ToArray();
    }

    private SupervisionCaller Supervisor() => new(_fixture.Create<string>(), IsAdmin: false, IsSupervisor: true);

    private static DiscoverySubmission Snapshot(DiscoverySubmission s) =>
        System.Text.Json.JsonSerializer.Deserialize<DiscoverySubmission>(System.Text.Json.JsonSerializer.Serialize(s))!;
}
