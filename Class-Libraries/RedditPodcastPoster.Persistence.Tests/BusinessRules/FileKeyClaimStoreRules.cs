using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.FileKeys;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class FileKeyClaimStoreRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly string _fileKey;
    private readonly Guid _ownerId;
    private readonly Guid _scopeId;

    public FileKeyClaimStoreRules()
    {
        _fileKey = "synthetic_watch";
        _ownerId = _fixture.CreateGuid();
        _scopeId = _fixture.CreateGuid();
        var created = _mocker.GetMock<ItemResponse<FileKeyClaim>>();
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<FileKeyClaim>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(created.Object);
    }

    private FileKeyClaimStore Store => _mocker.CreateInstance<FileKeyClaimStore>();

    private static CosmosException CosmosStatus(HttpStatusCode status) =>
        new("synthetic", status, 0, string.Empty, 0);

    [Fact(DisplayName =
        "The first claim of a file key is created in LookUps, because nothing else holds that key yet.")]
    public async Task first_claim_is_created()
    {
        // Arrange
        var store = Store;

        // Act
        var result = await store.TryClaim(_fileKey, _ownerId, _scopeId);

        // Assert
        result.Should().Be(FileKeyClaimResult.Created);
        _mocker.GetMock<Container>().Verify(c => c.CreateItemAsync(
            It.Is<FileKeyClaim>(claim =>
                claim.Id == FileKeyClaim.DocumentIdFor(_fileKey) &&
                claim.OwnerId == _ownerId &&
                claim.ScopeId == _scopeId &&
                claim.ClaimedUtc != default &&
                !claim.Committed),
            It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A file key another owner already claimed is refused, so the allocator moves on to the next suffix.")]
    public async Task claim_held_by_another_owner_is_refused()
    {
        // Arrange
        var other = _fixture.CreateGuid();
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<FileKeyClaim>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.Conflict));
        var held = new FileKeyClaim
        {
            Id = FileKeyClaim.DocumentIdFor(_fileKey),
            OwnerId = other,
            Committed = false
        };
        var response = Mock.Of<ItemResponse<FileKeyClaim>>(r => r.Resource == held);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<FileKeyClaim>(
                held.Id,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await Store.TryClaim(_fileKey, _ownerId, _scopeId);

        // Assert
        result.Should().Be(FileKeyClaimResult.HeldByOther);
    }

    [Fact(DisplayName =
        "A create that is never saved releases its file-key claim, so the document is deleted.")]
    public async Task unacknowledged_claim_is_released_with_the_scope()
    {
        // Arrange
        var store = Store;
        await store.TryClaim(_fileKey, _ownerId, _scopeId);
        var read = Mock.Of<ItemResponse<FileKeyClaim>>(r =>
            r.Resource == new FileKeyClaim
            {
                Id = FileKeyClaim.DocumentIdFor(_fileKey),
                OwnerId = _ownerId,
                ScopeId = _scopeId,
                Committed = false
            } &&
            r.ETag == _fixture.Create<string>());
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<FileKeyClaim>(
                FileKeyClaim.DocumentIdFor(_fileKey),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(read);

        // Act
        await store.ReleaseUnacknowledged(_scopeId);

        // Assert
        _mocker.GetMock<Container>().Verify(c => c.DeleteItemAsync<FileKeyClaim>(
            FileKeyClaim.DocumentIdFor(_fileKey),
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A second scope that observes the same owner's uncommitted claim and then ends leaves the document for the creating scope to acknowledge.")]
    public async Task second_scope_observing_an_uncommitted_claim_does_not_release_it()
    {
        // Arrange
        var store = Store;
        var otherScope = _fixture.CreateGuid();
        var creates = 0;
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<FileKeyClaim>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (creates++ > 0)
                {
                    throw CosmosStatus(HttpStatusCode.Conflict);
                }

                return Task.FromResult(_mocker.GetMock<ItemResponse<FileKeyClaim>>().Object);
            });
        var held = new FileKeyClaim
        {
            Id = FileKeyClaim.DocumentIdFor(_fileKey),
            OwnerId = _ownerId,
            ScopeId = _scopeId,
            ClaimedUtc = DateTime.UtcNow,
            Committed = false
        };
        var read = Mock.Of<ItemResponse<FileKeyClaim>>(r =>
            r.Resource == held &&
            r.ETag == _fixture.Create<string>());
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<FileKeyClaim>(
                held.Id,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(read);
        var replaced = _mocker.GetMock<ItemResponse<FileKeyClaim>>();
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<FileKeyClaim>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(replaced.Object);
        await store.TryClaim(_fileKey, _ownerId, _scopeId);

        // Act
        var observed = await store.TryClaim(_fileKey, _ownerId, otherScope);
        await store.ReleaseUnacknowledged(otherScope);
        await store.Acknowledge(_fileKey);

        // Assert
        observed.Should().Be(FileKeyClaimResult.AlreadyOurs);
        _mocker.GetMock<Container>().Verify(c => c.DeleteItemAsync<FileKeyClaim>(
            It.IsAny<string>(),
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.Is<FileKeyClaim>(claim =>
                claim.Committed &&
                claim.TimeToLiveSeconds == -1 &&
                claim.OwnerId == _ownerId),
            held.Id,
            It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A second scope that observes the same owner's uncommitted in-memory claim and then ends leaves that claim for the creating scope to acknowledge.")]
    public async Task second_scope_observing_an_in_memory_claim_does_not_release_it()
    {
        // Arrange
        var store = new InMemoryFileKeyClaimStore();
        var otherScope = _fixture.CreateGuid();
        var stranger = _fixture.CreateGuid();
        await store.TryClaim(_fileKey, _ownerId, _scopeId);

        // Act
        var observed = await store.TryClaim(_fileKey, _ownerId, otherScope);
        await store.ReleaseUnacknowledged(otherScope);
        var whileCreatorHoldsIt = await store.TryClaim(_fileKey, stranger, otherScope);
        await store.Acknowledge(_fileKey);
        await store.ReleaseUnacknowledged(_scopeId);
        var afterAcknowledge = await store.TryClaim(_fileKey, stranger, _scopeId);

        // Assert
        observed.Should().Be(FileKeyClaimResult.AlreadyOurs);
        whileCreatorHoldsIt.Should().Be(FileKeyClaimResult.HeldByOther);
        afterAcknowledge.Should().Be(FileKeyClaimResult.HeldByOther);
    }

    [Fact(DisplayName =
        "A second store that did not create a fresh uncommitted claim does not replace its scope id and does not delete it on dispose, so the creator keeps the lease.")]
    public async Task second_store_observing_a_fresh_uncommitted_claim_does_not_take_the_lease()
    {
        // Arrange
        var creator = Store;
        var observer = Store;
        var observerScope = _fixture.CreateGuid();
        var id = FileKeyClaim.DocumentIdFor(_fileKey);
        var creates = 0;
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<FileKeyClaim>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (creates++ > 0)
                {
                    throw CosmosStatus(HttpStatusCode.Conflict);
                }

                return Task.FromResult(_mocker.GetMock<ItemResponse<FileKeyClaim>>().Object);
            });
        var held = new FileKeyClaim
        {
            Id = id,
            OwnerId = _ownerId,
            ScopeId = _scopeId,
            ClaimedUtc = DateTime.UtcNow,
            Committed = false
        };
        var read = Mock.Of<ItemResponse<FileKeyClaim>>(r =>
            r.Resource == held &&
            r.ETag == _fixture.Create<string>());
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<FileKeyClaim>(
                id,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(read);
        var replaced = _mocker.GetMock<ItemResponse<FileKeyClaim>>();
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<FileKeyClaim>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(replaced.Object);
        await creator.TryClaim(_fileKey, _ownerId, _scopeId);

        // Act
        var observed = await observer.TryClaim(_fileKey, _ownerId, observerScope);
        await observer.ReleaseUnacknowledged(observerScope);

        // Assert
        observed.Should().Be(FileKeyClaimResult.AlreadyOurs);
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.IsAny<FileKeyClaim>(),
            It.IsAny<string>(),
            It.IsAny<PartitionKey?>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<Container>().Verify(c => c.DeleteItemAsync<FileKeyClaim>(
            It.IsAny<string>(),
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A claim older than the uncommitted lifetime is adopted with an etag replace, so the new scope can release it and the previous scope cannot.")]
    public async Task stale_uncommitted_claim_is_adopted_with_an_etag_replace()
    {
        // Arrange
        var store = Store;
        var previousScope = _fixture.CreateGuid();
        var etag = _fixture.Create<string>();
        var id = FileKeyClaim.DocumentIdFor(_fileKey);
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(
                It.IsAny<FileKeyClaim>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.Conflict));
        var before = new FileKeyClaim
        {
            Id = id,
            OwnerId = _ownerId,
            ScopeId = previousScope,
            ClaimedUtc = DateTime.UtcNow.AddSeconds(-(FileKeyClaim.UncommittedLifetimeSeconds + 60)),
            Committed = false
        };
        var after = new FileKeyClaim
        {
            Id = id,
            OwnerId = _ownerId,
            ScopeId = _scopeId,
            Committed = false
        };
        var reads = 0;
        var firstRead = Mock.Of<ItemResponse<FileKeyClaim>>(r => r.Resource == before && r.ETag == etag);
        var laterRead = Mock.Of<ItemResponse<FileKeyClaim>>(r => r.Resource == after && r.ETag == etag);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<FileKeyClaim>(
                id,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => reads++ == 0 ? firstRead : laterRead);
        var replaced = _mocker.GetMock<ItemResponse<FileKeyClaim>>();
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<FileKeyClaim>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(replaced.Object);

        // Act
        var result = await store.TryClaim(_fileKey, _ownerId, _scopeId);
        await store.ReleaseUnacknowledged(previousScope);
        await store.ReleaseUnacknowledged(_scopeId);

        // Assert
        result.Should().Be(FileKeyClaimResult.AlreadyOurs);
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.Is<FileKeyClaim>(claim =>
                claim.ScopeId == _scopeId &&
                claim.OwnerId == _ownerId &&
                !claim.Committed),
            id,
            It.IsAny<PartitionKey?>(),
            It.Is<ItemRequestOptions>(options => options.IfMatchEtag == etag),
            It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<Container>().Verify(c => c.DeleteItemAsync<FileKeyClaim>(
            id,
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Releasing a scope leaves a claim whose stored scope id now belongs to another scope, so a retry that took the lease is not deleted.")]
    public async Task release_leaves_a_claim_leased_to_another_scope()
    {
        // Arrange
        var store = Store;
        await store.TryClaim(_fileKey, _ownerId, _scopeId);
        var otherScope = _fixture.CreateGuid();
        var read = Mock.Of<ItemResponse<FileKeyClaim>>(r =>
            r.Resource == new FileKeyClaim
            {
                Id = FileKeyClaim.DocumentIdFor(_fileKey),
                OwnerId = _ownerId,
                ScopeId = otherScope,
                Committed = false
            } &&
            r.ETag == _fixture.Create<string>());
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<FileKeyClaim>(
                FileKeyClaim.DocumentIdFor(_fileKey),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(read);

        // Act
        await store.ReleaseUnacknowledged(_scopeId);

        // Assert
        _mocker.GetMock<Container>().Verify(c => c.DeleteItemAsync<FileKeyClaim>(
            It.IsAny<string>(),
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A throttled delete of a committed file-key claim surfaces the 429, so compensation can retry a key that would otherwise stay reserved.")]
    public async Task throttled_delete_surfaces_to_the_caller()
    {
        // Arrange
        var store = Store;
        var id = FileKeyClaim.DocumentIdFor(_fileKey);
        var read = Mock.Of<ItemResponse<FileKeyClaim>>(r =>
            r.Resource == new FileKeyClaim
            {
                Id = id,
                OwnerId = _ownerId,
                ScopeId = _scopeId,
                Committed = true,
                TimeToLiveSeconds = -1
            } &&
            r.ETag == _fixture.Create<string>());
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<FileKeyClaim>(
                id,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(read);
        _mocker.GetMock<Container>()
            .Setup(c => c.DeleteItemAsync<FileKeyClaim>(
                id,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.TooManyRequests));

        // Act
        var act = async () => await store.Delete(_fileKey, _ownerId);

        // Assert
        (await act.Should().ThrowAsync<CosmosException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
