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

public class ParentKindTransferLockStoreRules
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Guid _podcastId;
    private readonly DateTime _now;
    private readonly string _ourETag;
    private readonly string _theirETag;

    public ParentKindTransferLockStoreRules()
    {
        _podcastId = _fixture.CreateGuid();
        _now = _fixture.Create<DateTime>().ToUniversalTime();
        _ourETag = _fixture.Create<string>();
        _theirETag = _fixture.Create<string>();
        var created = _mocker.GetMock<ItemResponse<ParentKindTransferLock>>();
        created.Setup(r => r.ETag).Returns(_ourETag);
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<ParentKindTransferLock>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(created.Object);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<ParentKindTransferLock>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created.Object);
    }

    private static CosmosException CosmosStatus(HttpStatusCode status) => new("synthetic", status, 0, string.Empty, 0);

    private void ArrangeHeldMarker(DateTime startedUtc)
    {
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<ParentKindTransferLock>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.Conflict));
        var marker = new ParentKindTransferLock
        {
            Id = _podcastId,
            TargetKind = CatalogueParentKind.NewsOrganisation,
            StartedUtc = startedUtc,
            AttemptId = _fixture.CreateGuid()
        };
        var existing = Mock.Of<ItemResponse<ParentKindTransferLock>>(r => r.Resource == marker && r.ETag == _theirETag);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<ParentKindTransferLock>(
                _podcastId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
    }

    private Task<ParentKindTransferLockAcquisition> Acquire() =>
        _mocker.CreateInstance<ParentKindTransferLockStore>()
            .TryAcquire(_podcastId, CatalogueParentKind.TvShow, _now, StaleAfter);

    [Fact(DisplayName =
        "With no marker, the transfer creates one in LookUps under the podcast id (its own partition) and holds it.")]
    public async Task no_marker_is_created_and_held()
    {
        // Arrange
        var sut = _mocker.CreateInstance<ParentKindTransferLockStore>();

        // Act
        var acquisition = await sut.TryAcquire(_podcastId, CatalogueParentKind.TvShow, _now, StaleAfter);

        // Assert
        acquisition.Outcome.Should().Be(ParentKindTransferLockOutcome.Acquired);
        acquisition.Handle!.ETag.Should().Be(_ourETag);
        _mocker.GetMock<Container>().Verify(c => c.CreateItemAsync(
            It.Is<ParentKindTransferLock>(m => m.Id == _podcastId && m.StartedUtc == _now &&
                                               m.TargetKind == CatalogueParentKind.TvShow),
            new PartitionKey(_podcastId.ToString()),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A fresh marker means another transfer is running: the request is AlreadyRunning and the marker is not replaced.")]
    public async Task fresh_marker_is_already_running()
    {
        // Arrange
        ArrangeHeldMarker(_now - StaleAfter / 2);

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentKindTransferLockOutcome.AlreadyRunning);
        acquisition.Held.Should().BeFalse();
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.IsAny<ParentKindTransferLock>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A stale marker is replaced with If-Match on the etag that was read, so only one request can take it over and resume.")]
    public async Task stale_marker_is_taken_over_with_etag_check()
    {
        // Arrange
        ArrangeHeldMarker(_now - StaleAfter - TimeSpan.FromSeconds(1));

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentKindTransferLockOutcome.ResumedStale);
        acquisition.Handle!.ETag.Should().Be(_ourETag);
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.Is<ParentKindTransferLock>(m => m.Id == _podcastId && m.StartedUtc == _now),
            _podcastId.ToString(),
            new PartitionKey(_podcastId.ToString()),
            It.Is<ItemRequestOptions>(o => o.IfMatchEtag == _theirETag),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When another request takes over the same stale marker first (412 on the etag check), this request is AlreadyRunning.")]
    public async Task losing_the_takeover_race_is_already_running()
    {
        // Arrange
        ArrangeHeldMarker(_now - StaleAfter - TimeSpan.FromSeconds(1));
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<ParentKindTransferLock>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.PreconditionFailed));

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentKindTransferLockOutcome.AlreadyRunning);
        acquisition.Held.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Release deletes the marker with If-Match on this attempt's etag and does not throw when the marker was " +
        "taken over (412), because the new holder owns it now.")]
    public async Task release_is_etag_checked_and_tolerates_takeover()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(c => c.DeleteItemAsync<ParentKindTransferLock>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.PreconditionFailed));
        var handle = new ParentKindTransferLockHandle(_podcastId, _fixture.CreateGuid(), _ourETag);
        var sut = _mocker.CreateInstance<ParentKindTransferLockStore>();

        // Act
        var act = () => sut.Release(handle);

        // Assert
        await act.Should().NotThrowAsync();
        _mocker.GetMock<Container>().Verify(c => c.DeleteItemAsync<ParentKindTransferLock>(
            _podcastId.ToString(),
            new PartitionKey(_podcastId.ToString()),
            It.Is<ItemRequestOptions>(o => o.IfMatchEtag == _ourETag),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
