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

    private void ArrangeStoredRead(XOAuth2TokenState state) =>
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<XOAuth2TokenState>(XOAuth2TokenState._Id.ToString(), _partition,
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<XOAuth2TokenState>>(r => r.Resource == state && r.ETag == _writtenETag));

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
        "If the lease was lost before the save but the document still holds the refresh token this attempt spent, the rotated pair is still upserted, because X already invalidated the old refresh token.")]
    public async Task save_after_lost_lease_upserts()
    {
        // Arrange
        ArrangeReplaceThrows(HttpStatusCode.PreconditionFailed);
        ArrangeStoredRead(new XOAuth2TokenState
        {
            AccessToken = _current.State.AccessToken,
            RefreshToken = _current.State.RefreshToken,
            RefreshLeaseAttemptId = _fixture.CreateGuid(),
            RefreshLeaseUntilUtc = _now.AddMinutes(1)
        });
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

    [Fact(DisplayName =
        "SUGGEST 2: if the lease was lost and another host has since saved a newer pair, that pair is kept and returned instead of being overwritten.")]
    public async Task save_after_lost_lease_keeps_newer_pair()
    {
        // Arrange
        ArrangeReplaceThrows(HttpStatusCode.PreconditionFailed);
        var newer = new XOAuth2TokenState
        {
            AccessToken = _fixture.Create<string>(),
            RefreshToken = _fixture.Create<string>(),
            AccessTokenExpiresUtc = _now.AddHours(2)
        };
        ArrangeStoredRead(newer);
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var saved = await sut.SaveTokens(_current, _grant);

        // Assert
        saved.State.Should().BeSameAs(newer);
        _mocker.GetMock<Container>().Verify(c => c.UpsertItemAsync(It.IsAny<XOAuth2TokenState>(),
            It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "If the document was deleted before the save, the rotated pair is upserted.")]
    public async Task save_after_deleted_document_upserts()
    {
        // Arrange
        ArrangeReplaceThrows(HttpStatusCode.NotFound);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<XOAuth2TokenState>(XOAuth2TokenState._Id.ToString(), _partition,
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var saved = await sut.SaveTokens(_current, _grant);

        // Assert
        saved.State.RefreshToken.Should().Be(_grant.RefreshToken);
        _mocker.GetMock<Container>().Verify(c => c.UpsertItemAsync(It.IsAny<XOAuth2TokenState>(), _partition,
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "SUGGEST 3: saving a new pair clears a needs-reauth marker.")]
    public async Task save_clears_needs_reauth()
    {
        // Arrange
        _current.State.NeedsReauthSinceUtc = _now;
        _current.State.NeedsReauthReason = _fixture.Create<string>();
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var saved = await sut.SaveTokens(_current, _grant);

        // Assert
        saved.State.NeedsReauth.Should().BeFalse();
        saved.State.NeedsReauthReason.Should().BeNull();
    }

    [Fact(DisplayName =
        "SUGGEST 3: marking needs-reauth writes the marker and clears the lease, If-Match on the leased etag.")]
    public async Task mark_needs_reauth_is_if_match()
    {
        // Arrange
        var reason = _fixture.Create<string>();
        _current.State.RefreshLeaseAttemptId = _fixture.CreateGuid();
        _current.State.RefreshLeaseUntilUtc = _now.AddMinutes(1);
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var marked = await sut.MarkNeedsReauth(_current, reason, _now);

        // Assert
        marked.Should().BeTrue();
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(
            It.Is<XOAuth2TokenState>(s => s.NeedsReauthSinceUtc == _now && s.NeedsReauthReason == reason &&
                                          s.RefreshLeaseAttemptId == null && s.RefreshToken == _current.State.RefreshToken),
            XOAuth2TokenState._Id.ToString(), _partition,
            It.Is<ItemRequestOptions>(o => o.IfMatchEtag == _readETag), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "SUGGEST 3: marking needs-reauth after another host changed the document returns false.")]
    public async Task mark_needs_reauth_lost_race_returns_false()
    {
        // Arrange
        ArrangeReplaceThrows(HttpStatusCode.PreconditionFailed);
        var sut = _mocker.CreateInstance<XOAuth2TokenStore>();

        // Act
        var marked = await sut.MarkNeedsReauth(_current, _fixture.Create<string>(), _now);

        // Assert
        marked.Should().BeFalse();
    }

    [Fact(DisplayName =
        "SUGGEST 5: the token document is not IFileKeyed and serialises without fileKey, so it cannot join the file-keyed LookUps backup or publish.")]
    public void token_document_is_not_file_keyed()
    {
        // Arrange
        var state = _current.State;

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(state);

        // Assert
        state.Should().NotBeAssignableTo<RedditPodcastPoster.Models.Cosmos.IFileKeyed>();
        json.Should().NotContain("fileKey");
    }
}
