using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests;

public class D1SearchRebuilderTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly D1SearchOptions _options = new();
    private readonly List<string[]> _loads = [];
    private readonly string _rebuildId;
    private List<D1SearchCandidate> _catalogue = [];

    public D1SearchRebuilderTests()
    {
        _rebuildId = _fixture.CreateGuid().ToString();
        _options.Reconcile.DriftThreshold = 0.05;
        _options.Rebuild.Tolerance = 7;
        _mocker.Use<IOptions<D1SearchOptions>>(Options.Create(_options));
        _mocker.GetMock<ID1SearchIndexWriter>().SetupGet(w => w.IsActive).Returns(true);
        _mocker.GetMock<ID1SearchCatalogue>()
            .Setup(c => c.Read(0, It.IsAny<CancellationToken>()))
            .Returns(() => _catalogue.ToAsyncEnumerable());
        var client = _mocker.GetMock<ID1SearchRebuildClient>();
        client.Setup(c => c.Status(It.IsAny<CancellationToken>())).ReturnsAsync(Status(D1SearchRebuildStatus.None, null));
        client.Setup(c => c.Start(It.IsAny<CancellationToken>())).ReturnsAsync(_rebuildId);
        client.Setup(c => c.Load(_rebuildId, It.IsAny<IReadOnlyList<D1SearchDocument>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<D1SearchDocument> docs, CancellationToken _) =>
            {
                _loads.Add(docs.Select(d => d.Id).ToArray());
                return new D1SearchRebuildLoadOutcome(docs.Count, []);
            });
        client.Setup(c => c.Check(_rebuildId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Check(hits: 3));
        _mocker.GetMock<ID1SearchReconciler>()
            .Setup(r => r.Reconcile(It.IsAny<D1SearchReconcileRequest>(), It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<D1SearchReconcileProgress>?>()))
            .ReturnsAsync(Reconcile(D1SearchReconcileOutcome.Patched));
    }

    [Fact(DisplayName =
        "A rebuild loads every eligible document once, 100 per request, swaps with the loaded counts per kind, checks " +
        "the new index and drops the old one, because a clean rebuild must leave exactly one index behind.")]
    public async Task loads_swaps_checks_and_finishes()
    {
        // Arrange
        var episodes = Enumerable.Range(0, 200).Select(_ => Candidate()).ToList();
        var films = Enumerable.Range(0, 50).Select(_ => Candidate("Film")).ToList();
        var excluded = new D1SearchCandidate(_fixture.CreateGuid().ToString(), null);
        _catalogue = [..episodes, excluded, ..films, episodes[0]];
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.Rebuilt);
        report.Loaded.Should().Be(250);
        _loads.Select(l => l.Length).Should().Equal(100, 100, 50);
        _loads.SelectMany(l => l).Should().OnlyHaveUniqueItems();
        var client = _mocker.GetMock<ID1SearchRebuildClient>();
        client.Verify(c => c.Swap(_rebuildId, It.Is<D1SearchRebuildExpectation>(e =>
            e.ExpectedTotal == 250 && e.ExpectedKinds["Episode"] == 200 && e.ExpectedKinds["Film"] == 50 &&
            e.ExpectedKinds.Count == 2 && e.Tolerance == 7 && e.MaxShrink == 0.05), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.Finish(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.SwapBack(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.Abort(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ID1SearchReconciler>().Verify(r => r.Reconcile(
            new D1SearchReconcileRequest(true, false, true), It.IsAny<CancellationToken>(),
            It.IsAny<IProgress<D1SearchReconcileProgress>?>()), Times.Once);
    }

    [Fact(DisplayName =
        "A rebuild with an eligible playable that fails to build drops the shadow and never swaps, because the new " +
        "index would silently lose that document from search.")]
    public async Task build_failure_refuses_without_swapping()
    {
        // Arrange
        _catalogue = [Candidate(), new D1SearchCandidate(_fixture.CreateGuid().ToString(), null, BuildFailed: true)];
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.Refused);
        report.FailedToBuild.Should().Be(1);
        var client = _mocker.GetMock<ID1SearchRebuildClient>();
        client.Verify(c => c.Abort(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.Swap(It.IsAny<string>(), It.IsAny<D1SearchRebuildExpectation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A swap the Worker refuses after verification drops the shadow and reports why, because the live index was " +
        "never touched and the operator needs the failed checks.")]
    public async Task refused_swap_aborts()
    {
        // Arrange
        _catalogue = [Candidate()];
        var reason = _fixture.CreateTitle();
        _mocker.GetMock<ID1SearchRebuildClient>()
            .Setup(c => c.Swap(_rebuildId, It.IsAny<D1SearchRebuildExpectation>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new D1SearchHttpException(HttpStatusCode.Conflict, reason));
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.Refused);
        report.Problems.Should().ContainSingle().Which.Should().Contain(reason);
        _mocker.GetMock<ID1SearchRebuildClient>().Verify(c => c.Abort(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A sample search that finds nothing after the swap puts the old index back and drops the new one, without a " +
        "reconcile, because Jon's rule is that any failed check swaps straight back.")]
    public async Task failed_sample_swaps_back()
    {
        // Arrange
        _catalogue = [Candidate()];
        _mocker.GetMock<ID1SearchRebuildClient>()
            .Setup(c => c.Check(_rebuildId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Check(hits: 0));
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.SwappedBack);
        report.Problems.Should().ContainSingle().Which.Should().Contain("found nothing");
        var client = _mocker.GetMock<ID1SearchRebuildClient>();
        client.Verify(c => c.SwapBack(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.Abort(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.Finish(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ID1SearchReconciler>().Verify(r => r.Reconcile(It.IsAny<D1SearchReconcileRequest>(),
            It.IsAny<CancellationToken>(), It.IsAny<IProgress<D1SearchReconcileProgress>?>()), Times.Never);
    }

    [Theory(DisplayName =
        "A post-swap reconcile that refuses for drift or fails puts the old index back, because the reconcile is the " +
        "last check that the new index matches Cosmos.")]
    [InlineData(D1SearchReconcileOutcome.RefusedDrift)]
    [InlineData(D1SearchReconcileOutcome.Failed)]
    public async Task failed_reconcile_swaps_back(D1SearchReconcileOutcome outcome)
    {
        // Arrange
        _catalogue = [Candidate()];
        _mocker.GetMock<ID1SearchReconciler>()
            .Setup(r => r.Reconcile(It.IsAny<D1SearchReconcileRequest>(), It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<D1SearchReconcileProgress>?>()))
            .ReturnsAsync(Reconcile(outcome));
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.SwappedBack);
        _mocker.GetMock<ID1SearchRebuildClient>().Verify(c => c.SwapBack(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ID1SearchRebuildClient>().Verify(c => c.Finish(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A rebuild left loading by an interrupted run is aborted before a new one starts, because the Worker allows " +
        "only one rebuild and the live index was never changed.")]
    public async Task aborts_a_leftover_loading_rebuild()
    {
        // Arrange
        var leftover = _fixture.CreateGuid().ToString();
        _mocker.GetMock<ID1SearchRebuildClient>()
            .Setup(c => c.Status(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(D1SearchRebuildStatus.Loading, leftover));
        _catalogue = [Candidate()];
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.Rebuilt);
        var client = _mocker.GetMock<ID1SearchRebuildClient>();
        client.Verify(c => c.Abort(leftover, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.Start(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A rebuild an interrupted run had already swapped in is checked and settled without loading again, because " +
        "rerunning --rebuild is how the operator recovers.")]
    public async Task resumes_a_swapped_rebuild()
    {
        // Arrange
        _mocker.GetMock<ID1SearchRebuildClient>()
            .Setup(c => c.Status(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(D1SearchRebuildStatus.Swapped, _rebuildId));
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Should().BeEquivalentTo(new { Outcome = D1SearchRebuildOutcome.Rebuilt, Resumed = true, Loaded = 0 });
        var client = _mocker.GetMock<ID1SearchRebuildClient>();
        client.Verify(c => c.Start(It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.Finish(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ID1SearchCatalogue>().Verify(c => c.Read(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A swap whose answer is lost but which the Worker reports as committed carries on to the checks, because " +
        "aborting would fail and the new index is already live.")]
    public async Task lost_swap_answer_that_committed_continues()
    {
        // Arrange
        _catalogue = [Candidate()];
        var client = _mocker.GetMock<ID1SearchRebuildClient>();
        client.Setup(c => c.Swap(_rebuildId, It.IsAny<D1SearchRebuildExpectation>(), It.IsAny<CancellationToken>()))
            .Callback(() => client
                .Setup(c => c.Status(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Status(D1SearchRebuildStatus.Swapped, _rebuildId)))
            .ThrowsAsync(new HttpRequestException(_fixture.CreateTitle()));
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.Rebuilt);
        client.Verify(c => c.Finish(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A load that fails drops the shadow and never swaps, because a partly loaded index must not go live.")]
    public async Task load_failure_aborts()
    {
        // Arrange
        _catalogue = [Candidate()];
        var client = _mocker.GetMock<ID1SearchRebuildClient>();
        client.Setup(c => c.Load(_rebuildId, It.IsAny<IReadOnlyList<D1SearchDocument>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new D1SearchHttpException(HttpStatusCode.ServiceUnavailable, _fixture.CreateTitle()));
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.Failed);
        client.Verify(c => c.Abort(_rebuildId, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.Swap(It.IsAny<string>(), It.IsAny<D1SearchRebuildExpectation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A rebuild with the D1 sync unconfigured calls nothing, because the console must not touch an index it has no " +
        "credentials for.")]
    public async Task not_configured_does_nothing()
    {
        // Arrange
        _mocker.GetMock<ID1SearchIndexWriter>().SetupGet(w => w.IsActive).Returns(false);
        var sut = _mocker.CreateInstance<D1SearchRebuilder>();

        // Act
        var report = await sut.Rebuild(CancellationToken.None);

        // Assert
        report.Outcome.Should().Be(D1SearchRebuildOutcome.NotConfigured);
        _mocker.GetMock<ID1SearchRebuildClient>().Invocations.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "--rebuild needs an explicit --write and refuses --allow-drift and --full, because a rebuild always writes, " +
        "always reads the whole catalogue and has its own drift checks.")]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    public void command_line_rules(bool write, bool allowDrift, bool full, bool refused)
    {
        // Arrange
        var flags = (write, allowDrift, full);

        // Act
        var error = D1SearchRebuildCommandLine.Error(flags.write, flags.allowDrift, flags.full);

        // Assert
        (error is not null).Should().Be(refused);
    }

    private static D1SearchRebuildStatus Status(string state, string? id) =>
        new(state, id, new D1SearchRebuildCounts(0, null), null, null);

    private D1SearchRebuildCheck Check(int hits) =>
        new(hits > 0, new D1SearchRebuildIntegrity(true, null), new D1SearchRebuildCounts(1, null),
            [new D1SearchRebuildSample(_fixture.CreateTitle(), hits)]);

    private static D1SearchReconcileReport Reconcile(D1SearchReconcileOutcome outcome) =>
        new(outcome, 1, 1, 0, 0, 0, 0, 0);

    private D1SearchCandidate Candidate(string? kind = null)
    {
        var id = _fixture.CreateGuid().ToString();
        return new D1SearchCandidate(id, new D1SearchDocument
        {
            Id = id,
            ContentKind = kind,
            Title = _fixture.CreateTitle(),
            Release = DateTimeOffset.UtcNow,
            Duration = "00:10:00",
            ModifiedAt = 1
        });
    }
}
