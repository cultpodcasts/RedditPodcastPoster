using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryCurationSubmitServiceHeldReportTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IReadOnlyList<DiscoveryResultsDocument> _documents = [];

    public DiscoveryCurationSubmitServiceHeldReportTests()
    {
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDocuments(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(() => _documents);
    }

    [Fact(DisplayName =
        "When a curator submits a report awaiting supervisor review, the submit is refused as a conflict and nothing is submitted or marked processed, because held reports belong to the pending supervised submission.")]
    public async Task held_report_is_not_curatable()
    {
        // Arrange
        var report = new DiscoveryResultsDocument(DateTime.UtcNow, [])
        {
            SearchSince = _fixture.Create<string>(),
            State = DiscoveryResultsDocumentState.PendingSupervisor
        };
        _documents = [report];
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var result = await sut.SubmitAsync(
            new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = [report.Id] }, CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoveryCurationSubmitStatus.Conflict);
        _mocker.GetMock<IDiscoveryUrlSubmitter>().VerifyNoOtherCalls();
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.MarkAsProcessed(
            It.IsAny<Guid[]>(), It.IsAny<Guid[]>(), It.IsAny<Guid[]>()), Times.Never);
    }
}
