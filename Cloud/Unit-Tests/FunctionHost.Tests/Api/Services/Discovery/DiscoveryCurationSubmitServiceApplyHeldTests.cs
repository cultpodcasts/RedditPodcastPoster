using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryCurationSubmitServiceApplyHeldTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<string> _calls = [];
    private IReadOnlyList<Guid> _unclaimed = [];
    private bool _throwOnLookup;

    public DiscoveryCurationSubmitServiceApplyHeldTests()
    {
        var results = _mocker.GetMock<IDiscoveryResultsService>();
        results.Setup(s => s.TryClaimHeldForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("claimHeld"))
            .ReturnsAsync(() => _unclaimed);
        results.Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .Callback(() =>
            {
                _calls.Add("ingest");
                if (_throwOnLookup && _calls.Contains("claimHeld"))
                {
                    throw new InvalidOperationException();
                }
            })
            .ReturnsAsync([]);
    }

    [Fact(DisplayName =
        "INVARIANT: A supervisor apply may read results for a non-persisting language probe before the claim, then claims the held reports before the persisting ingest, and never uses the curator claim.")]
    public async Task apply_held_claims_before_ingest()
    {
        // Arrange
        var request = Request();
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.ApplyHeldAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        _calls.Should().Equal("ingest", "claimHeld", "ingest");
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.TryClaimForProcessing(
            It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.MarkAsProcessed(
            request.DiscoveryResultsDocumentIds, request.ResultIds, It.IsAny<Guid[]>()), Times.Once);
    }

    [Fact(DisplayName =
        "INVARIANT: Once the held reports are claimed, the ingest bookkeeping (search indexing) runs with a token the client cannot cancel, so a dropped request never turns a completed ingest into a failure.")]
    public async Task apply_held_ignores_client_cancellation_after_claim()
    {
        // Arrange
        var request = Request();
        using var client = new CancellationTokenSource();
        CancellationToken? indexToken = null;
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.TryClaimHeldForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => client.Cancel())
            .ReturnsAsync(() => _unclaimed);
        _mocker.GetMock<RedditPodcastPoster.EntitySearchIndexer.Services.IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<Guid> _, CancellationToken ct) => indexToken = ct)
            .ReturnsAsync(new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse());
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.ApplyHeldAsync(request, client.Token);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        indexToken.Should().NotBeNull();
        indexToken!.Value.CanBeCanceled.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a held report can no longer be claimed, the supervisor apply returns a conflict naming it and ingests nothing.")]
    public async Task apply_held_conflict_ingests_nothing()
    {
        // Arrange
        var request = Request();
        _unclaimed = [request.DiscoveryResultsDocumentIds[0]];
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.ApplyHeldAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Conflict);
        result.UnavailableDocumentIds.Should().Equal(request.DiscoveryResultsDocumentIds[0]);
        _calls.Should().Equal("ingest", "claimHeld");
        _mocker.GetMock<IDiscoveryUrlSubmitter>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "When a supervisor apply fails after claiming, the reports are handed back to PendingSupervisor, because a failed apply must leave the submission retryable.")]
    public async Task failed_apply_releases_held_claim()
    {
        // Arrange
        var request = Request();
        _throwOnLookup = true;
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.ApplyHeldAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Failed);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.ReleaseHeldClaim(
            request.DiscoveryResultsDocumentIds), Times.Once);
    }

    private DiscoverySubmitRequest Request() => new()
    {
        DiscoveryResultsDocumentIds = [_fixture.Create<Guid>()],
        ResultIds = [_fixture.Create<Guid>()]
    };
}
