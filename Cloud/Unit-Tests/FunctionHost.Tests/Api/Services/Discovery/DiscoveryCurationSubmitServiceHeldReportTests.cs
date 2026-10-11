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
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.MarkAsProcessed(
            It.IsAny<Guid[]>(), It.IsAny<Guid[]>(), It.IsAny<Guid[]>()), Times.Never);
    }

    [Fact(DisplayName =
        "A curator's submit claims every report before fetching or submitting any result, because claiming first is what stops a concurrent hold ingesting the same report.")]
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
        order.Should().StartWith(["claim", "fetch"]);
    }
}
