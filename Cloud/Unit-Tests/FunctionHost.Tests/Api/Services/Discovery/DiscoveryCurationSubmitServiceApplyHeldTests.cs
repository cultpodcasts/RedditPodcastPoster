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
                if (_throwOnLookup)
                {
                    throw new InvalidOperationException();
                }
            })
            .ReturnsAsync([]);
    }

    [Fact(DisplayName =
        "INVARIANT: A supervisor apply claims the held reports (PendingSupervisor to Processed, guarded) before any ingestion and never uses the curator claim, because only a supervisor apply may process a held report.")]
    public async Task apply_held_claims_before_ingest()
    {
        // Arrange
        var request = Request();
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.ApplyHeldAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        _calls.Should().Equal("claimHeld", "ingest");
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.TryClaimForProcessing(
            It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.MarkAsProcessed(
            request.DiscoveryResultsDocumentIds, request.ResultIds, It.IsAny<Guid[]>()), Times.Once);
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
        _calls.Should().Equal("claimHeld");
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
