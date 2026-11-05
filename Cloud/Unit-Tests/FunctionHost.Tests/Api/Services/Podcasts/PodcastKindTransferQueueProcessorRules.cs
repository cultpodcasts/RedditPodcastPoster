using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastKindTransferQueueProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<string> _phaseLog = [];
    private PodcastKindTransferResult _transferResult = null!;
    private Exception? _transferException;
    private Guid _podcastId;
    private DateTime _enqueuedUtc;

    public PodcastKindTransferQueueProcessorRules()
    {
        _podcastId = _fixture.CreateGuid();
        _enqueuedUtc = _fixture.Create<DateTime>().ToUniversalTime();
        _transferResult = new PodcastKindTransferResult(
            PodcastKindTransferStatus.Accepted, _podcastId, CatalogueParentKind.TvShow,
            _fixture.Create<int>());

        _mocker.GetMock<IPodcastKindTransferJobUpdater>()
            .Setup(u => u.MarkRunningAsync(
                It.IsAny<Guid>(), It.IsAny<CatalogueParentKind>(), It.IsAny<DateTime>()))
            .Callback<Guid, CatalogueParentKind, DateTime>((_, _, _) => _phaseLog.Add("MarkRunning"))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPodcastKindTransferJobUpdater>()
            .Setup(u => u.ApplyResultAsync(It.IsAny<PodcastKindTransferResult>(), It.IsAny<DateTime>()))
            .Callback<PodcastKindTransferResult, DateTime>((r, _) => _phaseLog.Add($"Apply:{r.Status}"))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPodcastKindTransferService>()
            .Setup(s => s.TransferAsync(
                It.IsAny<Guid>(), It.IsAny<PodcastKindTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (_transferException is not null)
                {
                    throw _transferException;
                }

                _phaseLog.Add("Transfer");
                return _transferResult;
            });
    }

    [Fact(DisplayName =
        "The queue processor marks the job Running, runs the transfer, then applies Accepted so the LookUps " +
        "document ends Completed.")]
    public async Task accepted_path_is_mark_transfer_apply()
    {
        // Arrange
        var message = new PodcastKindTransferQueueMessage
        {
            PodcastId = _podcastId,
            TargetKind = CatalogueParentKind.TvShow,
            EnqueuedUtc = _enqueuedUtc
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferQueueProcessor>();

        // Act
        await sut.ProcessAsync(message, CancellationToken.None);

        // Assert
        _phaseLog.Should().Equal("MarkRunning", "Transfer", "Apply:Accepted");
    }

    [Fact(DisplayName =
        "When the transfer returns AlreadyRunning, the processor still applies that result so the updater can " +
        "no-op without clobbering a terminal job.")]
    public async Task already_running_is_applied()
    {
        // Arrange
        _transferResult = new PodcastKindTransferResult(
            PodcastKindTransferStatus.AlreadyRunning, _podcastId, CatalogueParentKind.NewsOrganisation);
        var message = new PodcastKindTransferQueueMessage
        {
            PodcastId = _podcastId,
            TargetKind = CatalogueParentKind.NewsOrganisation,
            EnqueuedUtc = _enqueuedUtc
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferQueueProcessor>();

        // Act
        await sut.ProcessAsync(message, CancellationToken.None);

        // Assert
        _phaseLog.Should().Equal("MarkRunning", "Transfer", "Apply:AlreadyRunning");
    }

    [Fact(DisplayName =
        "When the transfer returns Failed, the processor applies Failed so GET reports the job as Failed.")]
    public async Task failed_transfer_is_applied()
    {
        // Arrange
        _transferResult = new PodcastKindTransferResult(
            PodcastKindTransferStatus.Failed, _podcastId, CatalogueParentKind.TvShow);
        var message = new PodcastKindTransferQueueMessage
        {
            PodcastId = _podcastId,
            TargetKind = CatalogueParentKind.TvShow,
            EnqueuedUtc = _enqueuedUtc
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferQueueProcessor>();

        // Act
        await sut.ProcessAsync(message, CancellationToken.None);

        // Assert
        _phaseLog.Should().Equal("MarkRunning", "Transfer", "Apply:Failed");
    }

    [Fact(DisplayName =
        "When TransferAsync throws, the processor applies a Failed result then rethrows so the host can retry " +
        "or poison the message without leaving the job stuck Running.")]
    public async Task transfer_exception_applies_failed_then_rethrows()
    {
        // Arrange
        _transferException = new InvalidOperationException(_fixture.Create<string>());
        var message = new PodcastKindTransferQueueMessage
        {
            PodcastId = _podcastId,
            TargetKind = CatalogueParentKind.TvShow,
            EnqueuedUtc = _enqueuedUtc
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferQueueProcessor>();

        // Act
        var act = () => sut.ProcessAsync(message, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _phaseLog.Should().Equal("MarkRunning", "Apply:Failed");
    }
}
