using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastKindTransferEnqueueServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryParentKindTransferLockStore _locks = new();
    private readonly List<ParentKindTransferJob> _savedJobs = [];
    private readonly List<PodcastKindTransferQueueMessage> _enqueued = [];
    private DateTime _now;
    private Podcast? _podcast;

    public PodcastKindTransferEnqueueServiceRules()
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
            .Setup(s => s.Save(It.IsAny<ParentKindTransferJob>()))
            .Callback<ParentKindTransferJob>(j => _savedJobs.Add(j))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPodcastKindTransferQueue>()
            .Setup(q => q.EnqueueAsync(It.IsAny<PodcastKindTransferQueueMessage>(), It.IsAny<CancellationToken>()))
            .Callback<PodcastKindTransferQueueMessage, CancellationToken>((m, _) => _enqueued.Add(m))
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "Enqueue validates the podcast, writes a Queued LookUps job, puts a queue message, and returns Queued " +
        "without running the transfer, so the HTTP request can finish before the move.")]
    public async Task enqueue_writes_queued_job_and_queue_message()
    {
        // Arrange
        var podcastId = _podcast!.Id;
        var sut = _mocker.CreateInstance<PodcastKindTransferEnqueueService>();

        // Act
        var result = await sut.EnqueueAsync(
            podcastId,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferEnqueueStatus.Queued);
        result.ParentId.Should().Be(podcastId);
        _savedJobs.Should().ContainSingle(j =>
            j.PodcastId == podcastId && j.Phase == ParentKindTransferJobPhase.Queued);
        _enqueued.Should().ContainSingle(m =>
            m.PodcastId == podcastId && m.TargetKind == CatalogueParentKind.TvShow);
        _locks.IsHeld(podcastId).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Enqueue refuses with AlreadyRunning when a fresh single-flight marker is held, without writing another " +
        "queue message, because the worker already owns the transfer.")]
    public async Task enqueue_refuses_when_fresh_lock_held()
    {
        // Arrange
        var podcastId = _podcast!.Id;
        _locks.SeedMarker(podcastId, _now, CatalogueParentKind.NewsOrganisation);
        var sut = _mocker.CreateInstance<PodcastKindTransferEnqueueService>();

        // Act
        var result = await sut.EnqueueAsync(
            podcastId,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.TvShow },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferEnqueueStatus.AlreadyRunning);
        _enqueued.Should().BeEmpty();
        _savedJobs.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Enqueue returns NotFound when the podcast row is missing, so curators are not given a Queued job for a " +
        "ghost id.")]
    public async Task enqueue_missing_podcast_is_not_found()
    {
        // Arrange
        _podcast = null;
        var podcastId = _fixture.CreateGuid();
        var sut = _mocker.CreateInstance<PodcastKindTransferEnqueueService>();

        // Act
        var result = await sut.EnqueueAsync(
            podcastId,
            new PodcastKindTransferRequest { TargetKind = CatalogueParentKind.NewsOrganisation },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastKindTransferEnqueueStatus.NotFound);
        _enqueued.Should().BeEmpty();
    }
}
