using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Social;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Auth;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Tests;

public class XOAuth2AccessTokenProviderRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _time;
    private readonly TwitterOptions _options;
    private readonly DateTime _now;
    private readonly string _storedAccessToken;
    private readonly string _storedRefreshToken;
    private readonly XOAuth2TokenGrant _refreshedGrant;

    public XOAuth2AccessTokenProviderRules()
    {
        _time = new FakeTimeProvider(_fixture.Create<DateTimeOffset>().ToUniversalTime());
        _now = _time.GetUtcNow().UtcDateTime;
        _options = new TwitterOptions {AuthMode = TwitterAuthMode.OAuth2};
        _storedAccessToken = _fixture.Create<string>();
        _storedRefreshToken = _fixture.Create<string>();
        _refreshedGrant = new XOAuth2TokenGrant(_fixture.Create<string>(), _fixture.Create<string>(),
            _now.AddHours(2), "tweet.read tweet.write users.read offline.access", _now, XOAuth2TokenClient.RefreshSource);
        _mocker.Use<TimeProvider>(_time);
        _mocker.Use(Options.Create(_options));
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.TryTakeRefreshLease(It.IsAny<XOAuth2TokenSnapshot?>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((XOAuth2TokenSnapshot? current, Guid attemptId, DateTime until, CancellationToken _) =>
                Snapshot(current!.State.AccessToken!, current.State.RefreshToken!,
                    current.State.AccessTokenExpiresUtc!.Value, attemptId, until));
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Success, _refreshedGrant));
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((XOAuth2TokenSnapshot leased, XOAuth2TokenGrant grant, CancellationToken _) =>
                Snapshot(grant.AccessToken, grant.RefreshToken, grant.AccessTokenExpiresUtc));
    }

    private XOAuth2TokenSnapshot Snapshot(string accessToken, string refreshToken, DateTime expiresUtc,
        Guid? leaseAttemptId = null, DateTime? leaseUntilUtc = null) =>
        new(new XOAuth2TokenState
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresUtc = expiresUtc,
            RefreshLeaseAttemptId = leaseAttemptId,
            RefreshLeaseUntilUtc = leaseUntilUtc
        }, _fixture.Create<string>());

    private void ArrangeStored(params XOAuth2TokenSnapshot?[] reads)
    {
        var sequence = _mocker.GetMock<IXOAuth2TokenStore>().SetupSequence(x => x.Get(It.IsAny<CancellationToken>()));
        foreach (var read in reads)
        {
            sequence = sequence.ReturnsAsync(read);
        }
    }

    [Fact(DisplayName =
        "A stored access token that is not near expiry is returned as-is, because refreshing needlessly spends the single-use refresh token.")]
    public async Task valid_token_is_returned_without_refresh()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddHours(1)));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().Be(_storedAccessToken);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Refresh rotation: a token inside the expiry skew is refreshed with the stored refresh token, and the rotated pair is saved before the new access token is handed out, because X invalidates the refresh token it was sent.")]
    public async Task near_expiry_token_refreshes_and_saves_rotated_pair_first()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken,
            _now + XOAuth2AccessTokenProvider.ExpirySkew - TimeSpan.FromSeconds(1)));
        var savedBeforeReturn = false;
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), _refreshedGrant, It.IsAny<CancellationToken>()))
            .Callback(() => savedBeforeReturn = true)
            .ReturnsAsync(Snapshot(_refreshedGrant.AccessToken, _refreshedGrant.RefreshToken,
                _refreshedGrant.AccessTokenExpiresUtc));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        savedBeforeReturn.Should().BeTrue();
        token.Should().Be(_refreshedGrant.AccessToken);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(_storedRefreshToken, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(
                It.Is<XOAuth2TokenSnapshot>(s => s.State.RefreshLeaseUntilUtc == _now + XOAuth2AccessTokenProvider.LeaseDuration),
                It.Is<XOAuth2TokenGrant>(g => g.RefreshToken == _refreshedGrant.RefreshToken),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Refresh rotation: the second refresh sends the refresh token saved by the first, never the spent one.")]
    public async Task second_refresh_uses_rotated_refresh_token()
    {
        // Arrange
        var expired = _now.AddMinutes(-1);
        ArrangeStored(
            Snapshot(_storedAccessToken, _storedRefreshToken, expired),
            Snapshot(_refreshedGrant.AccessToken, _refreshedGrant.RefreshToken, expired));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        await sut.GetAccessToken();
        await sut.GetAccessToken();

        // Assert
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(_storedRefreshToken, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(_refreshedGrant.RefreshToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Lock contention: while another host holds a live refresh lease, this host does not refresh; it waits and returns the token the other host saved.")]
    public async Task live_lease_waits_for_other_host_without_refreshing()
    {
        // Arrange
        var otherHostsToken = _fixture.Create<string>();
        ArrangeStored(
            Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1), _fixture.Create<Guid>(),
                _now.AddSeconds(30)),
            Snapshot(otherHostsToken, _fixture.Create<string>(), _now.AddHours(2)));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var pending = sut.GetAccessToken();
        _time.Advance(XOAuth2AccessTokenProvider.WaitInterval);
        var token = await pending;

        // Assert
        token.Should().Be(otherHostsToken);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.TryTakeRefreshLease(It.IsAny<XOAuth2TokenSnapshot?>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Lock contention: losing the If-Match race for the lease means another host is refreshing, so this host does not call X.")]
    public async Task lost_lease_race_does_not_refresh()
    {
        // Arrange
        var otherHostsToken = _fixture.Create<string>();
        ArrangeStored(
            Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)),
            Snapshot(otherHostsToken, _fixture.Create<string>(), _now.AddHours(2)));
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.TryTakeRefreshLease(It.IsAny<XOAuth2TokenSnapshot?>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((XOAuth2TokenSnapshot?) null);
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var pending = sut.GetAccessToken();
        _time.Advance(XOAuth2AccessTokenProvider.WaitInterval);
        var token = await pending;

        // Assert
        token.Should().Be(otherHostsToken);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Lock contention: an expired lease left by a crashed host is taken over, so one dead refresher cannot block posting.")]
    public async Task expired_lease_is_taken_over()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1), _fixture.Create<Guid>(),
            _now.AddSeconds(-1)));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().Be(_refreshedGrant.AccessToken);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(_storedRefreshToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A token X rejected with a 401 forces one refresh even though its stated expiry has not passed.")]
    public async Task rejected_token_forces_refresh()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddHours(1)));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken(_storedAccessToken);

        // Assert
        token.Should().Be(_refreshedGrant.AccessToken);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(_storedRefreshToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When the rejected token was already replaced by another host, the newer token is returned without another refresh.")]
    public async Task rejected_token_already_replaced_returns_newer_token()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddHours(1)));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken(_fixture.Create<string>());

        // Assert
        token.Should().Be(_storedAccessToken);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "With no token pair saved yet the provider returns null and calls nothing, because only the exchange console may create the first pair.")]
    public async Task no_saved_pair_returns_null()
    {
        // Arrange
        ArrangeStored((XOAuth2TokenSnapshot?) null);
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().BeNull();
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.ExchangeOAuth1(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact(DisplayName =
        "A refused refresh token (400 invalid_grant) is recovered by re-exchanging the OAuth 1.0a token while it is still configured.")]
    public async Task rejected_refresh_reexchanges_oauth1_token()
    {
        // Arrange
        _options.AccessToken = _fixture.Create<string>();
        _options.AccessTokenSecret = _fixture.Create<string>();
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Rejected, Error: "invalid_grant"));
        var exchanged = _refreshedGrant with {Source = XOAuth2TokenClient.ExchangeSource};
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.ExchangeOAuth1(_options.AccessToken, _options.AccessTokenSecret, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Success, exchanged));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().Be(exchanged.AccessToken);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), exchanged, It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact(DisplayName =
        "A failed refresh releases the lease and returns null, so the next caller can try again instead of waiting out the lease.")]
    public async Task failed_refresh_releases_lease()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Transient, Error: "server_error"));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().BeNull();
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.ReleaseRefreshLease(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<CancellationToken>()),
                Times.Once);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Saving the rotated pair is retried, because the refresh token X just issued is the only valid one.")]
    public async Task save_of_rotated_pair_is_retried()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        _mocker.GetMock<IXOAuth2TokenStore>()
            .SetupSequence(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic"))
            .ReturnsAsync(Snapshot(_refreshedGrant.AccessToken, _refreshedGrant.RefreshToken,
                _refreshedGrant.AccessTokenExpiresUtc));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().Be(_refreshedGrant.AccessToken);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), _refreshedGrant, It.IsAny<CancellationToken>()),
                Times.Exactly(2));
    }

    [Fact(DisplayName =
        "FIX 1: cancelling the X request (its 15 s timeout) while the refresh is in flight does not cut the refresh short; the rotated pair is still saved and returned.")]
    public async Task outer_cancellation_mid_refresh_still_saves_rotated_pair()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        using var requestTimeout = new CancellationTokenSource();
        CancellationToken refreshToken = default;
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, CancellationToken ct) =>
            {
                refreshToken = ct;
                requestTimeout.Cancel();
            })
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Success, _refreshedGrant));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken(null, requestTimeout.Token);

        // Assert
        requestTimeout.IsCancellationRequested.Should().BeTrue();
        refreshToken.CanBeCanceled.Should().BeFalse();
        token.Should().Be(_refreshedGrant.AccessToken);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), _refreshedGrant, CancellationToken.None),
                Times.Once);
    }

    [Fact(DisplayName =
        "FIX 1: the OAuth 1.0a re-exchange after a refused refresh is not cancellable by the X request either.")]
    public async Task reexchange_ignores_outer_cancellation()
    {
        // Arrange
        _options.AccessToken = _fixture.Create<string>();
        _options.AccessTokenSecret = _fixture.Create<string>();
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        using var requestTimeout = new CancellationTokenSource();
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => requestTimeout.Cancel())
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Rejected, Error: "invalid_grant"));
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.ExchangeOAuth1(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Success, _refreshedGrant));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken(null, requestTimeout.Token);

        // Assert
        token.Should().Be(_refreshedGrant.AccessToken);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.ExchangeOAuth1(_options.AccessToken, _options.AccessTokenSecret, CancellationToken.None),
                Times.Once);
    }

    [Theory(DisplayName =
        "FIX 1: the wait for another host's refresh is half the X request timeout (at least one second), so it ends before HttpClient.Timeout cancels the request.")]
    [InlineData(15, 7)]
    [InlineData(4, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 7)]
    public void wait_budget_follows_request_timeout(int requestTimeoutSeconds, int expectedWaits)
    {
        // Arrange
        _options.RequestTimeoutSeconds = requestTimeoutSeconds;

        // Act
        var waits = XOAuth2AccessTokenProvider.MaxWaits(_options);

        // Assert
        waits.Should().Be(expectedWaits);
        (XOAuth2AccessTokenProvider.WaitInterval * waits).Should()
            .BeLessThanOrEqualTo(TimeSpan.FromSeconds(Math.Max(1, requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 15)));
    }

    [Fact(DisplayName =
        "FIX 1: a request cancelled while waiting for another host's refresh gets a clean null instead of a TaskCanceledException out of Send.")]
    public async Task cancelled_wait_returns_null()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1), _fixture.Create<Guid>(),
            _now.AddSeconds(30)));
        using var requestTimeout = new CancellationTokenSource();
        await requestTimeout.CancelAsync();
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var act = async () => await sut.GetAccessToken(null, requestTimeout.Token);

        // Assert
        (await act.Should().NotThrowAsync()).Subject.Should().BeNull();
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "SUGGEST 3: a refused refresh with no OAuth 1.0a fallback marks the grant as needing re-authorisation instead of retrying on every request.")]
    public async Task refused_refresh_without_fallback_marks_needs_reauth()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Rejected, Error: "invalid_grant"));
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.MarkNeedsReauth(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<string?>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().BeNull();
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.MarkNeedsReauth(It.IsAny<XOAuth2TokenSnapshot>(), "invalid_grant", _now,
                It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.ExchangeOAuth1(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact(DisplayName =
        "SUGGEST 3: when the OAuth 1.0a re-exchange is refused too, the grant is marked as needing re-authorisation.")]
    public async Task refused_reexchange_marks_needs_reauth()
    {
        // Arrange
        _options.AccessToken = _fixture.Create<string>();
        _options.AccessTokenSecret = _fixture.Create<string>();
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Rejected, Error: "invalid_grant"));
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.ExchangeOAuth1(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Rejected, Error: "invalid_request"));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().BeNull();
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.MarkNeedsReauth(It.IsAny<XOAuth2TokenSnapshot>(), "invalid_request", _now,
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "SUGGEST 3: a grant marked as needing re-authorisation short-circuits to null with no token call and no lease.")]
    public async Task needs_reauth_marker_short_circuits()
    {
        // Arrange
        var marked = Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1));
        marked.State.NeedsReauthSinceUtc = _now.AddHours(-1);
        marked.State.NeedsReauthReason = "invalid_grant";
        ArrangeStored(marked);
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().BeNull();
        _mocker.GetMock<IXOAuth2TokenClient>().VerifyNoOtherCalls();
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.TryTakeRefreshLease(It.IsAny<XOAuth2TokenSnapshot?>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "SUGGEST 3: transient refresh failures never mark the grant, because the same refresh token may still work.")]
    public async Task transient_failure_does_not_mark_needs_reauth()
    {
        // Arrange
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.Refresh(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Transient, Error: "server_error"));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        await sut.GetAccessToken();

        // Assert
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.MarkNeedsReauth(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<string?>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "SUGGEST 2: when the store kept another host's newer pair, the provider hands out that pair's access token, not its own.")]
    public async Task returns_access_token_of_pair_actually_stored()
    {
        // Arrange
        var newerAccessToken = _fixture.Create<string>();
        ArrangeStored(Snapshot(_storedAccessToken, _storedRefreshToken, _now.AddMinutes(-1)));
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snapshot(newerAccessToken, _fixture.Create<string>(), _now.AddHours(2)));
        var sut = _mocker.CreateInstance<XOAuth2AccessTokenProvider>();

        // Act
        var token = await sut.GetAccessToken();

        // Assert
        token.Should().Be(newerAccessToken);
    }
}
