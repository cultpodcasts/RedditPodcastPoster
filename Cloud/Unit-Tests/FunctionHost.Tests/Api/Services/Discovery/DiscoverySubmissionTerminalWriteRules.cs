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
/// Business rules for the terminal write of a supervisor operation (apply, retry, release), from the real case:
/// submission 4a7d8838 (apply 13 Oct 22:57 BST) ingested all 11 results, then the client/Worker dropped the request
/// and the final ReplaceItemAsync threw "Cancellation Token has expired", leaving the submission Applying (locked).
/// Once an operation has claimed the submission, client cancellation must not abort the ingest or the final save.
/// </summary>
public class DiscoverySubmissionTerminalWriteRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<DiscoverySubmission> _replaced = [];
    private readonly List<CancellationToken> _ingestTokens = [];
    private readonly CancellationTokenSource _client = new();
    private DiscoverySubmission? _submission;
    private IReadOnlyList<DiscoveryResultsDocument> _documents = [];

    public DiscoverySubmissionTerminalWriteRules()
    {
        _mocker.Use(TimeProvider.System);
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _submission);
        // Like Cosmos: a write on a cancelled token throws instead of writing.
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.TryReplace(It.IsAny<DiscoverySubmission>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoverySubmission s, string _, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                _replaced.Add(Snapshot(s));
                return _fixture.Create<string>();
            });
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDocuments(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(() => _documents);
        // The client goes away while results are being ingested (after the claim).
        _mocker.GetMock<IDiscoveryCurationSubmitService>()
            .Setup(s => s.ApplyHeldAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoverySubmitRequest r, CancellationToken ct) => IngestThenClientCancels(r, ct));
        _mocker.GetMock<IDiscoveryCurationSubmitService>()
            .Setup(s => s.RetryItemsAsync(It.IsAny<DiscoverySubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoverySubmitRequest r, CancellationToken ct) => IngestThenClientCancels(r, ct));
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.TryTransitionState(It.IsAny<Guid>(), It.IsAny<DiscoveryResultsDocumentState>(),
                It.IsAny<DiscoveryResultsDocumentState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, DiscoveryResultsDocumentState _, DiscoveryResultsDocumentState _, CancellationToken ct) =>
            {
                _client.Cancel();
                ct.ThrowIfCancellationRequested();
                return true;
            });
    }

    [Fact(DisplayName =
        "INVARIANT (submission 4a7d8838): When the client cancels after every result was ingested, the apply still records Applied, clears the in-progress lock and records each item outcome, because the episodes exist and a stuck Applying submission needs an admin to unstick it.")]
    public async Task apply_completes_terminal_write_after_client_cancels()
    {
        // Arrange
        var resultIds = ArrangeHeldSubmission(11);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Apply(_submission!.Id,
            new DiscoverySubmissionApplyRequest { ETag = _submission.ETag!, AcceptedResultIds = resultIds },
            Supervisor(), _client.Token);

        // Assert
        _client.IsCancellationRequested.Should().BeTrue();
        result.Status.Should().Be(DiscoverySubmissionCommandStatus.Ok);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.Applied);
        AssertLockCleared(final);
        final.ItemOutcomes.Should().HaveCount(11).And
            .OnlyContain(x => x.State == DiscoverySubmissionItemState.Succeeded && x.EpisodeId != null);
        final.History.Select(h => h.Action).Should().EndWith(DiscoverySubmissionReviewService.AppliedAction);
    }

    [Fact(DisplayName =
        "INVARIANT: Once a supervisor apply has claimed the submission, the ingest is run with a token the client cannot cancel, so results are never half-ingested because a browser or proxy hung up.")]
    public async Task apply_ingest_ignores_client_cancellation()
    {
        // Arrange
        var resultIds = ArrangeHeldSubmission(2);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        await sut.Apply(_submission!.Id,
            new DiscoverySubmissionApplyRequest { ETag = _submission.ETag!, AcceptedResultIds = resultIds },
            Supervisor(), _client.Token);

        // Assert
        _ingestTokens.Should().ContainSingle().Which.CanBeCanceled.Should().BeFalse();
    }

    [Fact(DisplayName =
        "INVARIANT: When the client cancels during a retry of failed items, the retry still records the outcome (Applied when they succeed) and clears the lock.")]
    public async Task retry_completes_terminal_write_after_client_cancels()
    {
        // Arrange
        var failed = ArrangePartiallyApplied();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Retry(_submission!.Id,
            new DiscoverySubmissionRetryRequest { ETag = _submission.ETag! }, Supervisor(), _client.Token);

        // Assert
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Applied);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.Applied);
        AssertLockCleared(final);
        final.ItemOutcomes!.Single(x => x.ResultId == failed).State.Should().Be(DiscoverySubmissionItemState.Succeeded);
        _ingestTokens.Should().ContainSingle().Which.CanBeCanceled.Should().BeFalse();
    }

    [Fact(DisplayName =
        "INVARIANT: When the client cancels while a release is handing reports back, the release still finishes (reports Unprocessed, submission Released, lock cleared).")]
    public async Task release_completes_terminal_write_after_client_cancels()
    {
        // Arrange
        ArrangeHeldSubmission(2);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Release(_submission!.Id,
            new DiscoverySubmissionReleaseRequest { ETag = _submission.ETag! }, Supervisor(), _client.Token);

        // Assert
        _client.IsCancellationRequested.Should().BeTrue();
        result.SubmissionStatus.Should().Be(DiscoverySubmissionStatus.Released);
        var final = _replaced.Last();
        final.Status.Should().Be(DiscoverySubmissionStatus.Released);
        AssertLockCleared(final);
    }

    [Fact(DisplayName =
        "A client that has already gone before the claim gets nothing done: the claim write is still cancellable, so no lock is taken and nothing is ingested.")]
    public async Task cancelled_before_claim_takes_no_lock()
    {
        // Arrange
        var resultIds = ArrangeHeldSubmission(2);
        _client.Cancel();
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var act = () => sut.Apply(_submission!.Id,
            new DiscoverySubmissionApplyRequest { ETag = _submission.ETag!, AcceptedResultIds = resultIds },
            Supervisor(), _client.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _replaced.Should().BeEmpty();
        _ingestTokens.Should().BeEmpty();
    }

    private static void AssertLockCleared(DiscoverySubmission final)
    {
        final.InProgressOperation.Should().BeNull();
        final.InProgressSince.Should().BeNull();
        final.InProgressBy.Should().BeNull();
        final.InProgressResultIds.Should().BeNull();
        final.StatusBeforeInProgress.Should().BeNull();
    }

    private DiscoveryCurationSubmitResult IngestThenClientCancels(DiscoverySubmitRequest request, CancellationToken ct)
    {
        _ingestTokens.Add(ct);
        var outcome = new DiscoverySubmitOutcome("Success", false,
            request.ResultIds.Select(id =>
                new DiscoverySubmitItemOutcome(id, _fixture.Create<Guid>(), _fixture.Create<Guid>(), "Created")).ToArray(),
            new EntitySearchIndexerResponse());
        _client.Cancel();
        return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok, outcome);
    }

    private Guid[] ArrangeHeldSubmission(int count)
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

    private Guid ArrangePartiallyApplied()
    {
        var ids = ArrangeHeldSubmission(2);
        _documents[0].State = DiscoveryResultsDocumentState.Processed;
        _submission!.Status = DiscoverySubmissionStatus.PartiallyApplied;
        _submission.ItemOutcomes =
        [
            new DiscoverySubmissionItemOutcome { ResultId = ids[0], State = DiscoverySubmissionItemState.Succeeded, Attempts = 1 },
            new DiscoverySubmissionItemOutcome
            {
                ResultId = ids[1], State = DiscoverySubmissionItemState.Failed, Attempts = 1, Error = _fixture.Create<string>()
            }
        ];
        return ids[1];
    }

    private SupervisionCaller Supervisor() => new(_fixture.Create<string>(), IsAdmin: false, IsSupervisor: true);

    private static DiscoverySubmission Snapshot(DiscoverySubmission s) =>
        System.Text.Json.JsonSerializer.Deserialize<DiscoverySubmission>(System.Text.Json.JsonSerializer.Serialize(s))!;
}
