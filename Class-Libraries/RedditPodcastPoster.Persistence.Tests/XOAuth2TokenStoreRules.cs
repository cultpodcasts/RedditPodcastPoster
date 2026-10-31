using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Social;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Lookups;

namespace RedditPodcastPoster.Persistence.Tests;

public class XOAuth2TokenStoreRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly PartitionKey _partition = new(XOAuth2TokenState._Id.ToString());
    private readonly DateTime _now;
    private readonly string _readETag;
    private readonly string _writtenETag;
    private readonly XOAuth2TokenSnapshot _current;
    private readonly XOAuth2TokenGrant _grant;

    public XOAuth2TokenStoreRules()
    {
        _now = _fixture.Create<DateTime>().ToUniversalTime();
        _readETag = _fixture.Create<string>();
        _writtenETag = _fixture.Create<string>();
        _current = new XOAuth2TokenSnapshot(new XOAuth2TokenState
        {
            AccessToken = _fixture.Create<string>(),
            RefreshToken = _fixture.Create<string>(),
            AccessTokenExpiresUtc = _now
        }, _readETag);
        _grant = new XOAuth2TokenGrant(_fixture.Create<string>(), _fixture.Create<string>(), _now.AddHours(2),
            _fixture.Create<string>(), _now, _fixture.Create<string>());
        var written = Mock.Of<ItemResponse<XOAuth2TokenState>>(r => r.ETag == _writtenETag);
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(It.IsAny<XOAuth2TokenState>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(written);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(It.IsAny<XOAuth2TokenState>(), It.IsAny<string>(),
                It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(written);
        _mocker.GetMock<Container>()
            .Setup(c => c.UpsertItemAsync(It.IsAny<XOAuth2TokenState>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(written);
    }

    private static CosmosException CosmosStatus(HttpStatusCode status) => new("synthetic", status, 0, string.Empty, 0);

    private void ArrangeReplaceThrows(HttpStatusCode status) =>
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(It.IsAny<XOAuth2TokenState>(), It.IsAny<string>(),
                It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(status));

    [Fact(DisplayName = "With no token document saved yet, Get returns null instead of throwing.")]
    public async Task missing_document_reads_as_null()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<XOAuth2TokenState>(XOAuth2TokenState._Id.ToString(), _partition,
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var snapshot = await sut.Get();

        // Assert
        snapshot.Should().BeNull();
    }

    [Fact(DisplayName =
        "Taking the refresh lease replaces the document If-Match on the etag that was read, so only one refresher can win.")]
    public async Task lease_is_if_match_on_read_etag()
    {
        // Arrange
        var attemptId = _fixture.CreateGuid();
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var leased = await sut.TryTakeRefreshLease(_current, attemptId, _now.AddMinutes(1));

        // Assert
        leased!.ETag.Should().Be(_writtenETag);
        leased.State.RefreshLeaseAttemptId.Should().Be(attemptId);
        leased.State.RefreshToken.Should().Be(_current.State.RefreshToken);
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.Is<XOAuth2TokenState>(s => s.Id == XOAuth2TokenState._Id && s.RefreshLeaseUntilUtc == _now.AddMinutes(1)),
            XOAuth2TokenState._Id.ToString(), _partition,
            It.Is<ItemRequestOptions>(o => o.IfMatchEtag == _readETag), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Lock contention: losing the If-Match race (412) returns null, meaning another host is refreshing.")]
    public async Task lost_race_returns_null()
    {
        // Arrange
        ArrangeReplaceThrows(HttpStatusCode.PreconditionFailed);
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var leased = await sut.TryTakeRefreshLease(_current, _fixture.CreateGuid(), _now.AddMinutes(1));

        // Assert
        leased.Should().BeNull();
    }

    [Fact(DisplayName = "With no document yet the lease is a create, which a concurrent creator's 409 makes fail cleanly.")]
    public async Task first_lease_creates_document()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(c => c.CreateItemAsync(It.IsAny<XOAuth2TokenState>(), It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.Conflict));
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var leased = await sut.TryTakeRefreshLease(null, _fixture.CreateGuid(), _now.AddMinutes(1));

        // Assert
        leased.Should().BeNull();
        _mocker.GetMock<Container>().Verify(c => c.CreateItemAsync(It.IsAny<XOAuth2TokenState>(), _partition,
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Refresh rotation: saving the new pair replaces both tokens and clears the lease, If-Match on the leased etag.")]
    public async Task save_rotates_tokens_and_clears_lease()
    {
        // Arrange
        _current.State.RefreshLeaseAttemptId = _fixture.CreateGuid();
        _current.State.RefreshLeaseUntilUtc = _now.AddMinutes(1);
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var saved = await sut.SaveTokens(_current, _grant);

        // Assert
        saved.State.AccessToken.Should().Be(_grant.AccessToken);
        saved.State.RefreshToken.Should().Be(_grant.RefreshToken);
        saved.State.RefreshLeaseAttemptId.Should().BeNull();
        saved.State.RefreshLeaseUntilUtc.Should().BeNull();
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.Is<XOAuth2TokenState>(s => s.RefreshToken == _grant.RefreshToken), XOAuth2TokenState._Id.ToString(),
            _partition, It.Is<ItemRequestOptions>(o => o.IfMatchEtag == _readETag), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "If the lease was lost before the save, the rotated pair is still upserted, because X already invalidated the old refresh token.")]
    public async Task save_after_lost_lease_upserts()
    {
        // Arrange
        ArrangeReplaceThrows(HttpStatusCode.PreconditionFailed);
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var saved = await sut.SaveTokens(_current, _grant);

        // Assert
        saved.State.RefreshToken.Should().Be(_grant.RefreshToken);
        _mocker.GetMock<Container>().Verify(c => c.UpsertItemAsync(
            It.Is<XOAuth2TokenState>(s => s.RefreshToken == _grant.RefreshToken), _partition,
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Releasing a lease that was already taken over does not throw.")]
    public async Task release_of_lost_lease_does_not_throw()
    {
        // Arrange
        ArrangeReplaceThrows(HttpStatusCode.PreconditionFailed);
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var act = async () => await sut.ReleaseRefreshLease(_current);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
