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

    private void ArrangeHeldMarker(DateTime startedUtc, DateTime? heartbeatUtc = null)
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
            HeartbeatUtc = heartbeatUtc,
            AttemptId = _fixture.CreateGuid()
        };
        var existing = Mock.Of<ItemResponse<ParentKindTransferLock>>(r => r.Resource == marker && r.ETag == _theirETag);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<ParentKindTransferLock>(
                _podcastId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
    }

    private ParentKindTransferLockHandle Handle() =>
        new(_podcastId, _fixture.CreateGuid(), _ourETag, CatalogueParentKind.TvShow, _now - TimeSpan.FromMinutes(3));

    private void ArrangeCreateSequence(int conflictsBeforeSuccess)
    {
        var created = Mock.Of<ItemResponse<ParentKindTransferLock>>(r => r.ETag == _ourETag);
        var sequence = _mocker.GetMock<Container>()
            .SetupSequence(c => c.CreateItemAsync(
                It.IsAny<ParentKindTransferLock>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()));
        for (var i = 0; i < conflictsBeforeSuccess; i++)
        {
            sequence = sequence.ThrowsAsync(CosmosStatus(HttpStatusCode.Conflict));
        }

        sequence.ReturnsAsync(created);
    }

    private void ArrangeMarkerReadReturnsNotFound()
    {
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<ParentKindTransferLock>(
                _podcastId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));
    }

    private void VerifyCreates(int times) =>
        _mocker.GetMock<Container>().Verify(c => c.CreateItemAsync(
            It.IsAny<ParentKindTransferLock>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Exactly(times));

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
        acquisition.PreviousTargetKind.Should().Be(CatalogueParentKind.NewsOrganisation);
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
        var handle = Handle();
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

    [Fact(DisplayName =
        "A marker whose start is old but whose heartbeat is fresh is AlreadyRunning, because staleness is measured " +
        "from the last heartbeat and a long transfer keeps renewing it.")]
    public async Task fresh_heartbeat_is_already_running()
    {
        // Arrange
        ArrangeHeldMarker(_now - StaleAfter * 3, heartbeatUtc: _now - StaleAfter / 2);

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentKindTransferLockOutcome.AlreadyRunning);
    }

    [Fact(DisplayName =
        "When the marker is released between a 409 on create and the read (404), the second pass creates it and holds it.")]
    public async Task released_between_create_and_read_is_created_on_second_pass()
    {
        // Arrange
        ArrangeCreateSequence(conflictsBeforeSuccess: 1);
        ArrangeMarkerReadReturnsNotFound();

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentKindTransferLockOutcome.Acquired);
        acquisition.Handle!.ETag.Should().Be(_ourETag);
        VerifyCreates(2);
    }

    [Fact(DisplayName =
        "When a stale marker is released between the read and the etag-checked replace (404), the next pass creates " +
        "a fresh marker and holds it.")]
    public async Task stale_marker_released_before_replace_is_created_fresh()
    {
        // Arrange
        ArrangeHeldMarker(_now - StaleAfter - TimeSpan.FromSeconds(1));
        ArrangeCreateSequence(conflictsBeforeSuccess: 1);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<ParentKindTransferLock>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentKindTransferLockOutcome.Acquired);
        VerifyCreates(2);
    }

    [Fact(DisplayName =
        "When both passes find the marker vanishing and reappearing (409 then 404, twice), the request gives up as " +
        "AlreadyRunning instead of looping.")]
    public async Task both_passes_used_up_is_already_running()
    {
        // Arrange
        ArrangeCreateSequence(conflictsBeforeSuccess: 2);
        ArrangeMarkerReadReturnsNotFound();

        // Act
        var acquisition = await Acquire();

        // Assert
        acquisition.Outcome.Should().Be(ParentKindTransferLockOutcome.AlreadyRunning);
        acquisition.Held.Should().BeFalse();
        VerifyCreates(2);
    }

    [Fact(DisplayName =
        "Release swallows an unexpected (non-Cosmos) error, because the transfer outcome stands and a leftover " +
        "marker goes stale.")]
    public async Task release_swallows_unexpected_errors()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(c => c.DeleteItemAsync<ParentKindTransferLock>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));
        var sut = _mocker.CreateInstance<ParentKindTransferLockStore>();

        // Act
        var act = () => sut.Release(Handle());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "Renew writes the heartbeat with If-Match on this attempt's etag, keeps the start time and attempt, and " +
        "returns the handle with the new etag.")]
    public async Task renew_writes_heartbeat_with_etag_check()
    {
        // Arrange
        var renewedETag = _fixture.Create<string>();
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<ParentKindTransferLock>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<ParentKindTransferLock>>(r => r.ETag == renewedETag));
        var handle = Handle();
        var sut = _mocker.CreateInstance<ParentKindTransferLockStore>();

        // Act
        var renewed = await sut.Renew(handle, _now);

        // Assert
        renewed.Should().Be(handle with { ETag = renewedETag });
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.Is<ParentKindTransferLock>(m => m.Id == _podcastId && m.HeartbeatUtc == _now &&
                                               m.StartedUtc == handle.StartedUtc && m.AttemptId == handle.AttemptId &&
                                               m.TargetKind == handle.TargetKind),
            _podcastId.ToString(),
            new PartitionKey(_podcastId.ToString()),
            It.Is<ItemRequestOptions>(o => o.IfMatchEtag == _ourETag),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Renew returns null (lease lost) when the marker was taken over (412), so the holder stops writing.")]
    public async Task renew_after_takeover_is_lost()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<ParentKindTransferLock>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.PreconditionFailed));
        var sut = _mocker.CreateInstance<ParentKindTransferLockStore>();

        // Act
        var renewed = await sut.Renew(Handle(), _now);

        // Assert
        renewed.Should().BeNull();
    }
}
