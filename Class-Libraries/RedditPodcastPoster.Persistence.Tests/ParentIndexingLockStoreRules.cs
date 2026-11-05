using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Lookups;

namespace RedditPodcastPoster.Persistence.Tests;

public class ParentIndexingLockStoreRules
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Guid _parentId;
    private readonly Guid _lockId;
    private readonly DateTime _now;
    private readonly string _ourETag;
    private readonly string _theirETag;

    public ParentIndexingLockStoreRules()
    {
        _parentId = _fixture.CreateGuid();
        _lockId = ParentIndexingLock.IdFor(_parentId);
        _now = _fixture.Create<DateTime>().ToUniversalTime();
        _ourETag = _fixture.Create<string>();
        _theirETag = _fixture.Create<string>();
        var created = _mocker.GetMock<ItemResponse<ParentIndexingLock>>();
        created.Setup(r => r.ETag).Returns(_ourETag);
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<ParentIndexingLock>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(created.Object);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<ParentIndexingLock>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created.Object);
    }

    private static CosmosException CosmosStatus(HttpStatusCode status) => new("synthetic", status, 0, string.Empty, 0);

    private void ArrangeHeldMarker(DateTime startedUtc, DateTime? heartbeatUtc = null)
    {
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<ParentIndexingLock>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.Conflict));
        var marker = new ParentIndexingLock
        {
            Id = _lockId,
            ParentId = _parentId,
            Source = ParentIndexingLock.SourceHourly,
            StartedUtc = startedUtc,
            HeartbeatUtc = heartbeatUtc,
            AttemptId = _fixture.CreateGuid()
        };
        var existing = Mock.Of<ItemResponse<ParentIndexingLock>>(r => r.Resource == marker && r.ETag == _theirETag);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<ParentIndexingLock>(
                _lockId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
    }

    private Task<ParentIndexingLockAcquisition> Acquire() =>
        _mocker.CreateInstance<ParentIndexingLockStore>()
            .TryAcquire(_parentId, _now, StaleAfter, ParentIndexingLock.SourceOnDemand);

    [Fact(DisplayName =
        "With no marker, the index creates one in LookUps under the xor id (not the parent id) and holds it.")]
    public async Task no_marker_is_created_and_held()
    {
        // Arrange
        var sut = _mocker.CreateInstance<ParentIndexingLockStore>();

        // Act
        var acquisition = await sut.TryAcquire(_parentId, _now, StaleAfter, ParentIndexingLock.SourceOnDemand);

        // Assert
        acquisition.Outcome.Should().Be(ParentIndexingLockOutcome.Acquired);
        acquisition.Handle!.ETag.Should().Be(_ourETag);
        acquisition.Handle.ParentId.Should().Be(_parentId);
        _mocker.GetMock<Container>().Verify(c => c.CreateItemAsync(
            It.Is<ParentIndexingLock>(m =>
                m.Id == _lockId && m.ParentId == _parentId && m.StartedUtc == _now &&
                m.Source == ParentIndexingLock.SourceOnDemand),
            new PartitionKey(_lockId.ToString()),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A fresh marker means another index is running: the request is AlreadyRunning and the marker is not replaced.")]
    public async Task fresh_marker_is_already_running()
    {
        // Arrange
        ArrangeHeldMarker(_now - StaleAfter / 2);

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentIndexingLockOutcome.AlreadyRunning);
        acquisition.Held.Should().BeFalse();
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.IsAny<ParentIndexingLock>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A stale marker is replaced with If-Match on the etag that was read, so only one request can take it over.")]
    public async Task stale_marker_is_taken_over_with_etag_check()
    {
        // Arrange
        ArrangeHeldMarker(_now - StaleAfter - TimeSpan.FromSeconds(1));

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentIndexingLockOutcome.ResumedStale);
        acquisition.Handle!.ETag.Should().Be(_ourETag);
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.Is<ParentIndexingLock>(m => m.Id == _lockId && m.ParentId == _parentId && m.StartedUtc == _now),
            _lockId.ToString(),
            new PartitionKey(_lockId.ToString()),
            It.Is<ItemRequestOptions>(o => o.IfMatchEtag == _theirETag),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "TryGet returns the marker for the parent when present, keyed by the xor Cosmos id.")]
    public async Task try_get_reads_xor_id()
    {
        // Arrange
        var marker = new ParentIndexingLock
        {
            Id = _lockId,
            ParentId = _parentId,
            StartedUtc = _now,
            AttemptId = _fixture.CreateGuid(),
            Source = ParentIndexingLock.SourceHourly
        };
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<ParentIndexingLock>(
                _lockId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<ParentIndexingLock>>(r => r.Resource == marker));
        var sut = _mocker.CreateInstance<ParentIndexingLockStore>();

        // Act
        var found = await sut.TryGet(_parentId);

        // Assert
        found.Should().BeSameAs(marker);
    }

    [Fact(DisplayName =
        "TryGet returns null when no marker exists for the parent.")]
    public async Task try_get_missing_is_null()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<ParentIndexingLock>(
                _lockId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));
        var sut = _mocker.CreateInstance<ParentIndexingLockStore>();

        // Act
        var found = await sut.TryGet(_parentId);

        // Assert
        found.Should().BeNull();
    }
}
