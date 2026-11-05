using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastKindTransferJobUpdaterRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<ParentKindTransferJob> _savedJobs = [];
    private ParentKindTransferJob? _existingJob;
    private DateTime _now;

    public PodcastKindTransferJobUpdaterRules()
    {
        _now = _fixture.Create<DateTime>().ToUniversalTime();
        _mocker.GetMock<TimeProvider>()
            .Setup(t => t.GetUtcNow())
            .Returns(() => new DateTimeOffset(_now, TimeSpan.Zero));
        _mocker.GetMock<IParentKindTransferJobStore>()
            .Setup(s => s.Get(It.IsAny<Guid>()))
            .ReturnsAsync(() => _existingJob);
        _mocker.GetMock<IParentKindTransferJobStore>()
            .Setup(s => s.Save(It.IsAny<ParentKindTransferJob>()))
            .Callback<ParentKindTransferJob>(j => _savedJobs.Add(j))
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "ApplyResult Accepted writes a Completed job with CompletedUtc, so GET can report the transfer finished.")]
    public async Task accepted_maps_to_completed()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var enqueuedUtc = _now.AddMinutes(-5);
        var playableCount = _fixture.Create<int>();
        var sut = _mocker.CreateInstance<PodcastKindTransferJobUpdater>();

        // Act
        await sut.ApplyResultAsync(
            new PodcastKindTransferResult(
                PodcastKindTransferStatus.Accepted, podcastId, CatalogueParentKind.TvShow, playableCount),
            enqueuedUtc);

        // Assert
        _savedJobs.Should().ContainSingle(j =>
            j.PodcastId == podcastId &&
            j.Phase == ParentKindTransferJobPhase.Completed &&
            j.CompletedUtc == _now &&
            j.PlayableCount == playableCount);
    }

    [Fact(DisplayName =
        "ApplyResult AlreadyRunning is a no-op when the LookUps job is already Completed, so a late duplicate " +
        "worker that lost the lock cannot clobber a finished transfer into eternal Running.")]
    public async Task already_running_leaves_completed_job_untouched()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var completedUtc = _now.AddMinutes(-2);
        _existingJob = new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = CatalogueParentKind.NewsOrganisation,
            Phase = ParentKindTransferJobPhase.Completed,
            EnqueuedUtc = _now.AddMinutes(-10),
            StartedUtc = _now.AddMinutes(-8),
            CompletedUtc = completedUtc,
            PlayableCount = _fixture.Create<int>()
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferJobUpdater>();

        // Act
        await sut.ApplyResultAsync(
            new PodcastKindTransferResult(
                PodcastKindTransferStatus.AlreadyRunning, podcastId, CatalogueParentKind.NewsOrganisation),
            _existingJob.EnqueuedUtc);

        // Assert
        _savedJobs.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "ApplyResult AlreadyRunning does not write a Running job with CompletedUtc, even when no terminal job " +
        "exists yet, because that combination left GET stuck on Running forever.")]
    public async Task already_running_never_saves_running_with_completed_utc()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        _existingJob = new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = CatalogueParentKind.TvShow,
            Phase = ParentKindTransferJobPhase.Queued,
            EnqueuedUtc = _now.AddMinutes(-1)
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferJobUpdater>();

        // Act
        await sut.ApplyResultAsync(
            new PodcastKindTransferResult(
                PodcastKindTransferStatus.AlreadyRunning, podcastId, CatalogueParentKind.TvShow),
            _existingJob.EnqueuedUtc);

        // Assert
        _savedJobs.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "ApplyResult Failed writes a Failed job with a failure reason.")]
    public async Task failed_maps_to_failed_phase()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var sut = _mocker.CreateInstance<PodcastKindTransferJobUpdater>();

        // Act
        await sut.ApplyResultAsync(
            new PodcastKindTransferResult(
                PodcastKindTransferStatus.Failed, podcastId, CatalogueParentKind.TvShow),
            _now);

        // Assert
        _savedJobs.Should().ContainSingle(j =>
            j.Phase == ParentKindTransferJobPhase.Failed &&
            j.FailureReason != null &&
            j.CompletedUtc == _now);
    }
}
