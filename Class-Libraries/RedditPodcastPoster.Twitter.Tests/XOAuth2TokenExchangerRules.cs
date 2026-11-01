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

public class XOAuth2TokenExchangerRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _time;
    private readonly TwitterOptions _options;
    private readonly DateTime _now;
    private readonly XOAuth2TokenSnapshot _saved;
    private readonly XOAuth2TokenGrant _grant;

    public XOAuth2TokenExchangerRules()
    {
        _time = new FakeTimeProvider(_fixture.Create<DateTimeOffset>().ToUniversalTime());
        _now = _time.GetUtcNow().UtcDateTime;
        _options = new TwitterOptions
        {
            AccessToken = _fixture.Create<string>(),
            AccessTokenSecret = _fixture.Create<string>(),
            OAuth2ClientId = _fixture.Create<string>(),
            OAuth2ClientSecret = _fixture.Create<string>()
        };
        _saved = new XOAuth2TokenSnapshot(new XOAuth2TokenState
        {
            AccessToken = _fixture.Create<string>(),
            RefreshToken = _fixture.Create<string>(),
            AccessTokenExpiresUtc = _now.AddHours(1),
            UpdatedUtc = _now.AddHours(-1),
            Source = XOAuth2TokenClient.ExchangeSource
        }, _fixture.Create<string>());
        _grant = new XOAuth2TokenGrant(_fixture.Create<string>(), _fixture.Create<string>(), _now.AddHours(2),
            "tweet.read tweet.write users.read offline.access", _now, XOAuth2TokenClient.ExchangeSource);
        _mocker.Use<TimeProvider>(_time);
        _mocker.Use(Options.Create(_options));
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_saved);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.TryTakeRefreshLease(It.IsAny<XOAuth2TokenSnapshot?>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_saved);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_saved);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.ExchangeOAuth1(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Success, _grant));
    }

    private void AssertNoSecretsIn(XOAuth2TokenExchangeResult result)
    {
        var text = string.Join("\n", result.Report);
        foreach (var secret in new[]
                     {
                         _options.AccessToken, _options.AccessTokenSecret, _options.OAuth2ClientId,
                         _options.OAuth2ClientSecret, _saved.State.AccessToken, _saved.State.RefreshToken,
                         _grant.AccessToken, _grant.RefreshToken
                     }
                     .OfType<string>())
        {
            text.Should().NotContain(secret);
        }
    }

    [Fact(DisplayName =
        "Dry run (the default) calls nothing on X and writes nothing, reports what it would do, and prints no token or secret.")]
    public async Task dry_run_sends_and_writes_nothing()
    {
        // Arrange
        var sut = _mocker.CreateInstance<XOAuth2TokenExchanger>();

        // Act
        var result = await sut.Run(false);

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenExchangeOutcome.DryRun);
        result.Succeeded.Should().BeTrue();
        result.Report.Should().Contain(l => l.Contains("dry run"))
            .And.Contain(l => l.Contains("invalidates"));
        _mocker.GetMock<IXOAuth2TokenClient>().VerifyNoOtherCalls();
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.TryTakeRefreshLease(It.IsAny<XOAuth2TokenSnapshot?>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()), Times.Never);
        AssertNoSecretsIn(result);
    }

    [Fact(DisplayName =
        "Dry run with a missing setting reports which one by name only and fails, so Jon can fix it before executing.")]
    public async Task dry_run_reports_missing_settings()
    {
        // Arrange
        _options.OAuth2ClientSecret = null;
        var sut = _mocker.CreateInstance<XOAuth2TokenExchanger>();

        // Act
        var result = await sut.Run(false);

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenExchangeOutcome.MissingConfiguration);
        result.Succeeded.Should().BeFalse();
        result.Report.Should().Contain("twitter:OAuth2ClientSecret: MISSING");
        _mocker.GetMock<IXOAuth2TokenClient>().VerifyNoOtherCalls();
        AssertNoSecretsIn(result);
    }

    [Fact(DisplayName =
        "Execute takes the refresh lease, exchanges the OAuth 1.0a token once and saves the new pair, without printing it.")]
    public async Task execute_exchanges_and_saves()
    {
        // Arrange
        var sut = _mocker.CreateInstance<XOAuth2TokenExchanger>();

        // Act
        var result = await sut.Run(true);

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenExchangeOutcome.Exchanged);
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Verify(x => x.ExchangeOAuth1(_options.AccessToken!, _options.AccessTokenSecret!,
                It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(_saved, _grant, It.IsAny<CancellationToken>()), Times.Once);
        result.Report.Should().NotContain(l => l.StartsWith("WARNING"));
        AssertNoSecretsIn(result);
    }

    [Fact(DisplayName = "Execute warns when X grants fewer scopes than posting needs.")]
    public async Task execute_warns_on_missing_scopes()
    {
        // Arrange
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.ExchangeOAuth1(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Success, _grant with {Scope = "tweet.read"}));
        var sut = _mocker.CreateInstance<XOAuth2TokenExchanger>();

        // Act
        var result = await sut.Run(true);

        // Assert
        result.Report.Should().Contain(l => l.StartsWith("WARNING") && l.Contains("tweet.write") && l.Contains("offline.access"));
    }

    [Fact(DisplayName =
        "Execute refuses while another host holds a live refresh lease, because a second exchange would invalidate that host's new pair.")]
    public async Task execute_refuses_while_lease_is_held()
    {
        // Arrange
        _saved.State.RefreshLeaseAttemptId = _fixture.Create<Guid>();
        _saved.State.RefreshLeaseUntilUtc = _now.AddSeconds(30);
        var sut = _mocker.CreateInstance<XOAuth2TokenExchanger>();

        // Act
        var result = await sut.Run(true);

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenExchangeOutcome.RefreshInProgress);
        _mocker.GetMock<IXOAuth2TokenClient>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "A failed exchange releases the lease and saves nothing.")]
    public async Task failed_exchange_releases_lease()
    {
        // Arrange
        _mocker.GetMock<IXOAuth2TokenClient>()
            .Setup(x => x.ExchangeOAuth1(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Rejected, Error: "invalid_grant"));
        var sut = _mocker.CreateInstance<XOAuth2TokenExchanger>();

        // Act
        var result = await sut.Run(true);

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenExchangeOutcome.ExchangeFailed);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.ReleaseRefreshLease(_saved, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "SUGGEST 6: a save that keeps failing after a successful exchange is retried, then reported as SaveFailed (exit 1) instead of throwing.")]
    public async Task failed_save_after_exchange_is_retried_and_reported()
    {
        // Arrange
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Setup(x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic"));
        var sut = _mocker.CreateInstance<XOAuth2TokenExchanger>();

        // Act
        var result = await sut.Run(true);

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenExchangeOutcome.SaveFailed);
        result.Succeeded.Should().BeFalse();
        result.Report.Should().Contain(l => l.Contains("NOT saved"));
        _mocker.GetMock<IXOAuth2TokenStore>()
            .Verify(x => x.SaveTokens(_saved, _grant, CancellationToken.None),
                Times.Exactly(XOAuth2TokenPersistence.SaveAttempts));
        AssertNoSecretsIn(result);
    }

    [Fact(DisplayName = "SUGGEST 3: the dry run reports a grant marked as needing re-authorisation.")]
    public async Task dry_run_reports_needs_reauth()
    {
        // Arrange
        _saved.State.NeedsReauthSinceUtc = _now.AddHours(-1);
        _saved.State.NeedsReauthReason = "invalid_grant";
        var sut = _mocker.CreateInstance<XOAuth2TokenExchanger>();

        // Act
        var result = await sut.Run(false);

        // Assert
        result.Report.Should().Contain(l => l.Contains("NEEDS RE-AUTHORISATION") && l.Contains("invalid_grant"));
    }
}
