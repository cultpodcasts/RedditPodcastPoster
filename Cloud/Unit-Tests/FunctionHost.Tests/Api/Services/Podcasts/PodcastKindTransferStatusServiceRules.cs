using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastKindTransferStatusServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryParentKindTransferLockStore _locks = new();
    private DateTime _now;
    private Podcast? _podcast;
    private ParentKindTransferJob? _job;
    private TvShow? _tvShow;
    private NewsOrganisation? _news;

    public PodcastKindTransferStatusServiceRules()
    {
        _now = _fixture.Create<DateTime>().ToUniversalTime();
        _podcast = _fixture.CreatePodcast();
        _mocker.Use<IParentKindTransferLockStore>(_locks);
        _mocker.GetMock<TimeProvider>()
            .Setup(t => t.GetUtcNow())
            .Returns(() => new DateTimeOffset(_now, TimeSpan.Zero));
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(It.IsAny<Guid>()))
            .ReturnsAsync(() => _podcast);
        _mocker.GetMock<IParentKindTransferJobStore>()
            .Setup(s => s.Get(It.IsAny<Guid>()))
            .ReturnsAsync(() => _job);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync(() => _tvShow);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync(() => _news);
    }

    [Fact(DisplayName =
        "Status is Running when a fresh single-flight marker is held, even if the job document still says Queued, " +
        "because the lock is the authoritative in-flight signal.")]
    public async Task fresh_lock_is_running()
    {
        // Arrange
        var podcastId = _podcast!.Id;
        _locks.SeedMarker(podcastId, _now, CatalogueParentKind.TvShow);
        _job = new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = CatalogueParentKind.TvShow,
            Phase = ParentKindTransferJobPhase.Queued,
            EnqueuedUtc = _now
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferStatusService>();

        // Act
        var result = await sut.GetAsync(podcastId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferJobQueryStatus.Running);
        result.TargetKind.Should().Be(CatalogueParentKind.TvShow);
    }

    [Fact(DisplayName =
        "Status is Queued when the LookUps job is Queued and no fresh lock is held.")]
    public async Task queued_job_without_lock_is_queued()
    {
        // Arrange
        var podcastId = _podcast!.Id;
        _job = new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = CatalogueParentKind.NewsOrganisation,
            Phase = ParentKindTransferJobPhase.Queued,
            EnqueuedUtc = _now
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferStatusService>();

        // Act
        var result = await sut.GetAsync(podcastId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferJobQueryStatus.Queued);
    }

    [Fact(DisplayName =
        "Status is Completed when the podcast is gone, no job remains, and a same-id TV show exists, " +
        "so clients can still learn the transfer finished after job documents age out.")]
    public async Task dest_tv_show_without_podcast_is_completed()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        _podcast = null;
        _job = null;
        _tvShow = _fixture.Create<TvShow>();
        _tvShow.Id = podcastId;
        var sut = _mocker.CreateInstance<PodcastKindTransferStatusService>();

        // Act
        var result = await sut.GetAsync(podcastId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferJobQueryStatus.Completed);
        result.TargetKind.Should().Be(CatalogueParentKind.TvShow);
    }

    [Fact(DisplayName =
        "Status is Failed when a Queued job is older than the transfer lock window and no lock is held, " +
        "because the worker never started and the podcast was not moved.")]
    public async Task stale_queued_job_is_failed()
    {
        // Arrange
        var podcastId = _podcast!.Id;
        _job = new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = CatalogueParentKind.NewsOrganisation,
            Phase = ParentKindTransferJobPhase.Queued,
            EnqueuedUtc = _now - PodcastKindTransferService.LockStaleAfter
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferStatusService>();

        // Act
        var result = await sut.GetAsync(podcastId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferJobQueryStatus.Failed);
        result.FailureReason.Should().Be(
            "The transfer was queued but the worker never started. The podcast was not moved.");
    }

    [Fact(DisplayName =
        "Status is Completed when the podcast is gone, no job remains, and a same-id news organisation exists, " +
        "so clients can still learn the transfer finished after job documents age out.")]
    public async Task dest_parent_without_podcast_is_completed()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        _podcast = null;
        _job = null;
        _news = _fixture.Create<NewsOrganisation>();
        _news.Id = podcastId;
        var sut = _mocker.CreateInstance<PodcastKindTransferStatusService>();

        // Act
        var result = await sut.GetAsync(podcastId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferJobQueryStatus.Completed);
        result.TargetKind.Should().Be(CatalogueParentKind.NewsOrganisation);
    }

    [Fact(DisplayName =
        "Status is Idle when the podcast still exists and there is no job or lock.")]
    public async Task podcast_without_job_is_idle()
    {
        // Arrange
        var podcastId = _podcast!.Id;
        var sut = _mocker.CreateInstance<PodcastKindTransferStatusService>();

        // Act
        var result = await sut.GetAsync(podcastId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferJobQueryStatus.Idle);
    }

    [Fact(DisplayName =
        "Status is Failed (not Running) when the LookUps job says Running but no fresh single-flight marker " +
        "exists, so a crashed worker cannot leave the curator polling Running forever.")]
    public async Task stale_running_job_without_lock_is_failed()
    {
        // Arrange
        var podcastId = _podcast!.Id;
        _job = new ParentKindTransferJob
        {
            PodcastId = podcastId,
            TargetKind = CatalogueParentKind.TvShow,
            Phase = ParentKindTransferJobPhase.Running,
            EnqueuedUtc = _now.AddMinutes(-20),
            StartedUtc = _now.AddMinutes(-20)
        };
        var sut = _mocker.CreateInstance<PodcastKindTransferStatusService>();

        // Act
        var result = await sut.GetAsync(podcastId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferJobQueryStatus.Failed);
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
        result.TargetKind.Should().Be(CatalogueParentKind.TvShow);
    }
}
