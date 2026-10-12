using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryCurationSubmitServiceRetryTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IReadOnlyList<DiscoveryResultsDocument> _documents = [];
    private IReadOnlyList<DiscoveryResult> _results = [];

    public DiscoveryCurationSubmitServiceRetryTests()
    {
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDocuments(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(() => _documents);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync(() => _results);
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new DiscoverySubmitResult(DiscoverySubmitResultState.NoUrls));
    }

    [Fact(DisplayName =
        "Retrying items of Processed reports resubmits them without claiming or changing report state, records the failure reason of an item that throws, and restamps only the retried results.")]
    public async Task retry_resubmits_and_restamps_only_given_results()
    {
        // Arrange
        var (report, ok, bad) = ArrangeProcessedReport();
        var reason = _fixture.Create<string>();
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(s => s.Submit(It.Is<DiscoveryResult>(r => r.Id == bad.Id), It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new InvalidOperationException(reason));
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.RetryItemsAsync(
            new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = [report.Id], ResultIds = [ok.Id, bad.Id] },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        result.Outcome!.Results.Single(x => x.DiscoveryItemId == bad.Id).Error.Should().Contain(reason);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.UpdateResultStates(
            It.Is<Guid[]>(x => x.SequenceEqual(new[] { report.Id })),
            It.Is<Guid[]>(x => x.SequenceEqual(new[] { ok.Id })),
            It.Is<Guid[]>(x => x.SequenceEqual(new[] { bad.Id }))), Times.Once);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.TryClaimForProcessing(
            It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.TryClaimHeldForProcessing(
            It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.MarkAsProcessed(
            It.IsAny<Guid[]>(), It.IsAny<Guid[]>(), It.IsAny<Guid[]>()), Times.Never);
    }

    [Fact(DisplayName =
        "Retry refuses as a conflict when a report is not Processed, and submits nothing, because retry only finishes an applied submission.")]
    public async Task retry_refuses_unprocessed_report()
    {
        // Arrange
        var (report, ok, _) = ArrangeProcessedReport();
        report.State = DiscoveryResultsDocumentState.PendingSupervisor;
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.RetryItemsAsync(
            new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = [report.Id], ResultIds = [ok.Id] },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Conflict);
        _mocker.GetMock<IDiscoveryUrlSubmitter>().VerifyNoOtherCalls();
    }

    private (DiscoveryResultsDocument Report, DiscoveryResult Ok, DiscoveryResult Bad) ArrangeProcessedReport()
    {
        var ok = new DiscoveryResult { Id = _fixture.Create<Guid>() };
        var bad = new DiscoveryResult { Id = _fixture.Create<Guid>() };
        var report = new DiscoveryResultsDocument(DateTime.UtcNow.AddDays(-1), [ok, bad])
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.Processed
        };
        _documents = [report];
        _results = [ok, bad];
        return (report, ok, bad);
    }
}
