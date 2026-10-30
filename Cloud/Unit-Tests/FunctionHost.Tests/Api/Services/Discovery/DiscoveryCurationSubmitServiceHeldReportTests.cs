using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryCurationSubmitServiceHeldReportTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IReadOnlyList<Guid> _unclaimed = [];

    public DiscoveryCurationSubmitServiceHeldReportTests()
    {
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.TryClaimForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _unclaimed);
    }

    [Fact(DisplayName =
        "When a curator's submit loses the claim on a report (held, processed or changed concurrently), the submit is a conflict naming it and nothing is ingested or stamped, because hold and submit must be mutually exclusive.")]
    public async Task losing_claim_ingests_nothing()
    {
        // Arrange
        var documentId = _fixture.Create<Guid>();
        _unclaimed = [documentId];
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.SubmitAsync(
            new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = [documentId], ResultIds = [_fixture.Create<Guid>()] },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Conflict);
        result.UnavailableDocumentIds.Should().Equal(documentId);
        _mocker.GetMock<IDiscoveryUrlSubmitter>().VerifyNoOtherCalls();
        _mocker.GetMock<IEpisodeSearchIndexerService>().VerifyNoOtherCalls();
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()), Times.Once);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.MarkAsProcessed(
            It.IsAny<Guid[]>(), It.IsAny<Guid[]>(), It.IsAny<Guid[]>(),
            It.IsAny<IReadOnlyList<DiscoverySubmitItemOutcome>>()), Times.Never);
    }

    [Fact(DisplayName =
        "A curator's submit reads results for the language check before claiming, and the persisting submit still runs only after the claim, because an unsupported language must not mark the report processed.")]
    public async Task claims_before_submitting()
    {
        // Arrange
        var order = new List<string>();
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.TryClaimForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("claim"))
            .ReturnsAsync(() => _unclaimed);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .Callback(() => order.Add("fetch"))
            .ReturnsAsync([]);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        await sut.SubmitAsync(new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = [_fixture.Create<Guid>()] },
            CancellationToken.None);

        // Assert
        order.Should().StartWith(["fetch", "claim", "fetch"]);
    }

    [Fact(DisplayName =
        "When the submit fails after claiming but before any episode submit is attempted, the claimed reports are released to Unprocessed, because nothing was ingested and the curator must be able to retry.")]
    public async Task failure_before_ingestion_releases_claim()
    {
        // Arrange
        var documentIds = new[] { _fixture.Create<Guid>() };
        var fetches = 0;
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .Returns(() =>
            {
                fetches++;
                if (fetches > 1)
                {
                    throw new InvalidOperationException();
                }

                return Task.FromResult<IEnumerable<RedditPodcastPoster.Models.Discovery.DiscoveryResult>>([]);
            });
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.SubmitAsync(new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = documentIds },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Failed);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.ReleaseClaim(documentIds), Times.Once);
        _mocker.GetMock<IDiscoveryUrlSubmitter>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "When the submit fails after an episode submit was attempted, the claimed reports stay Processed, because releasing them would let the same episodes be ingested twice.")]
    public async Task failure_after_ingestion_keeps_claim()
    {
        // Arrange
        var documentIds = new[] { _fixture.Create<Guid>() };
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync([new RedditPodcastPoster.Models.Discovery.DiscoveryResult { Id = _fixture.Create<Guid>() }]);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.MarkAsProcessed(It.IsAny<Guid[]>(), It.IsAny<Guid[]>(), It.IsAny<Guid[]>(),
                It.IsAny<IReadOnlyList<DiscoverySubmitItemOutcome>>()))
            .ThrowsAsync(new InvalidOperationException());
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.SubmitAsync(new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = documentIds },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Failed);
        _mocker.GetMock<IDiscoveryUrlSubmitter>().Verify(s => s.Submit(
            It.IsAny<RedditPodcastPoster.Models.Discovery.DiscoveryResult>(),
            It.IsAny<RedditPodcastPoster.PodcastServices.Abstractions.Models.IndexingContext>(),
            It.Is<RedditPodcastPoster.UrlSubmission.Models.SubmitOptions>(o => !o.LanguageProbe)), Times.Once);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.ReleaseClaim(It.IsAny<Guid[]>()), Times.Never);
    }
}
