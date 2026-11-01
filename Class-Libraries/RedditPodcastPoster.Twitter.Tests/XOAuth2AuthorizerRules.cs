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

public class XOAuth2AuthorizerRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _time;
    private readonly TwitterOptions _options;
    private readonly DateTime _now;
    private readonly XOAuth2TokenSnapshot _saved;
    private readonly XOAuth2TokenGrant _grant;
    private readonly Uri _redirect = new("http://127.0.0.1:8739/callback");

    public XOAuth2AuthorizerRules()
    {
        _time = new FakeTimeProvider(_fixture.Create<DateTimeOffset>().ToUniversalTime());
        _now = _time.GetUtcNow().UtcDateTime;
        _options = new TwitterOptions
        {
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
            "tweet.read tweet.write users.read offline.access", _now, XOAuth2TokenClient.AuthorizationSource);
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
            .Setup(x => x.ExchangeAuthorizationCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XOAuth2TokenResult(XOAuth2TokenOutcome.Success, _grant));
    }

    private void AssertNoSecretsIn(IReadOnlyList<string> report, XOAuth2AuthorizationSession? session = null)
    {
        var text = string.Join("\n", report);
        foreach (var secret in new[]
                     {
                         _options.OAuth2ClientId, _options.OAuth2ClientSecret, _saved.State.AccessToken,
                         _saved.State.RefreshToken, _grant.AccessToken, _grant.RefreshToken, session?.CodeVerifier
                     }
                     .OfType<string>())
        {
            text.Should().NotContain(secret);
        }
    }

    [Fact(DisplayName =
        "Dry run calls nothing on X and writes nothing, and the report names the PKCE login without printing secrets.")]
    public async Task dry_run_sends_and_writes_nothing()
    {
        // Arrange
        var sut = _mocker.CreateInstance<XOAuth2Authorizer>();

        // Act
        var result = await sut.Begin(execute: false, _redirect);

        // Assert
        result.Outcome.Should().Be(XOAuth2AuthorizeOutcome.DryRun);
        result.Session.Should().BeNull();
        AssertNoSecretsIn(result.Report);
        _mocker.GetMock<IXOAuth2TokenClient>().Verify(
            x => x.ExchangeAuthorizationCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IXOAuth2TokenStore>().Verify(
            x => x.TryTakeRefreshLease(It.IsAny<XOAuth2TokenSnapshot?>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Execute holds the refresh lease and returns an authorize URL that carries the challenge but not the verifier.")]
    public async Task execute_returns_a_url_without_the_verifier()
    {
        // Arrange
        var sut = _mocker.CreateInstance<XOAuth2Authorizer>();

        // Act
        var result = await sut.Begin(execute: true, _redirect);

        // Assert
        result.Outcome.Should().Be(XOAuth2AuthorizeOutcome.AwaitingConsent);
        result.Session!.AuthorizeUrl.Query.Should().NotContain(result.Session.CodeVerifier);
        result.Session.AuthorizeUrl.Query.Should().Contain("code_challenge_method=S256");
        AssertNoSecretsIn(result.Report, result.Session);
        _mocker.GetMock<IXOAuth2TokenClient>().Verify(
            x => x.ExchangeAuthorizationCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A matching callback saves the new pair and posts the verifier with the code. The report still has neither.")]
    public async Task matching_callback_saves_the_pair()
    {
        // Arrange
        var sut = _mocker.CreateInstance<XOAuth2Authorizer>();
        var begun = await sut.Begin(execute: true, _redirect);
        var code = _fixture.Create<string>();

        // Act
        var result = await sut.Complete(begun.Session!,
            $"http://127.0.0.1:8739/callback?code={Uri.EscapeDataString(code)}&state={begun.Session!.State}");

        // Assert
        result.Outcome.Should().Be(XOAuth2AuthorizeOutcome.Authorised);
        AssertNoSecretsIn(result.Report, begun.Session);
        result.Report.Should().NotContain(line => line.Contains(code));
        _mocker.GetMock<IXOAuth2TokenClient>().Verify(x => x.ExchangeAuthorizationCode(
            code, begun.Session.CodeVerifier, _redirect.AbsoluteUri, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IXOAuth2TokenStore>().Verify(x => x.SaveTokens(
            _saved, _grant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A callback whose state does not match this login saves nothing and releases the refresh lease.")]
    public async Task mismatched_state_saves_nothing()
    {
        // Arrange
        var sut = _mocker.CreateInstance<XOAuth2Authorizer>();
        var begun = await sut.Begin(execute: true, _redirect);

        // Act
        var result = await sut.Complete(begun.Session!,
            "http://127.0.0.1:8739/callback?code=someone-else&state=not-this-login");

        // Assert
        result.Outcome.Should().Be(XOAuth2AuthorizeOutcome.ConsentFailed);
        _mocker.GetMock<IXOAuth2TokenClient>().Verify(
            x => x.ExchangeAuthorizationCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IXOAuth2TokenStore>().Verify(
            x => x.ReleaseRefreshLease(_saved, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IXOAuth2TokenStore>().Verify(
            x => x.SaveTokens(It.IsAny<XOAuth2TokenSnapshot>(), It.IsAny<XOAuth2TokenGrant>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "A non-loopback redirect is refused before any lease is taken.")]
    public async Task remote_redirect_is_refused()
    {
        // Arrange
        var sut = _mocker.CreateInstance<XOAuth2Authorizer>();

        // Act
        var result = await sut.Begin(execute: true, new Uri("https://example.com/callback"));

        // Assert
        result.Outcome.Should().Be(XOAuth2AuthorizeOutcome.MissingConfiguration);
        result.Session.Should().BeNull();
        _mocker.GetMock<IXOAuth2TokenStore>().Verify(
            x => x.TryTakeRefreshLease(It.IsAny<XOAuth2TokenSnapshot?>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }
}
