using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Lookups;

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
}
