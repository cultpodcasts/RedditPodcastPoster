using System.Net;
using System.Text;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq.AutoMock;
using RedditPodcastPoster.Twitter.Auth;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Tests.Fakes;

namespace RedditPodcastPoster.Twitter.Tests;

public class XOAuth2TokenClientRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly ScriptedHttpHandler _x = new();
    private readonly FakeTimeProvider _time;
    private readonly TwitterOptions _options;

    public XOAuth2TokenClientRules()
    {
        _time = new FakeTimeProvider(_fixture.Create<DateTimeOffset>().ToUniversalTime());
        _options = new TwitterOptions
        {
            OAuth2ClientId = _fixture.Create<string>(),
            OAuth2ClientSecret = _fixture.Create<string>()
        };
        _mocker.Use<TimeProvider>(_time);
        _mocker.Use(Options.Create(_options));
        _mocker.Use(new HttpClient(_x) {BaseAddress = _options.ApiBaseAddress});
    }

    private string TokenJson(string access, string refresh, int expiresIn) =>
        $"{{\"token_type\":\"bearer\",\"access_token\":\"{access}\",\"refresh_token\":\"{refresh}\",\"expires_in\":{expiresIn},\"scope\":\"tweet.write offline.access\"}}";

    [Fact(DisplayName =
        "Refresh posts grant_type=refresh_token to https://api.x.com/2/oauth2/token with Basic client authentication and returns the rotated pair.")]
    public async Task refresh_posts_refresh_grant_with_basic_auth()
    {
        // Arrange
        var refreshToken = _fixture.Create<string>();
        var newAccess = _fixture.Create<string>();
        var newRefresh = _fixture.Create<string>();
        _x.Respond(HttpStatusCode.OK, TokenJson(newAccess, newRefresh, 7200));
        var sut = _mocker.CreateInstance<XOAuth2TokenClient>();

        // Act
        var result = await sut.Refresh(refreshToken);

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenOutcome.Success);
        result.Grant!.AccessToken.Should().Be(newAccess);
        result.Grant.RefreshToken.Should().Be(newRefresh);
        result.Grant.AccessTokenExpiresUtc.Should().Be(_time.GetUtcNow().UtcDateTime.AddSeconds(7200));
        result.Grant.Source.Should().Be(XOAuth2TokenClient.RefreshSource);
        var request = _x.Requests.Single();
        request.Uri.Should().Be(new Uri("https://api.x.com/2/oauth2/token"));
        request.Authorization!.Scheme.Should().Be("Basic");
        Encoding.UTF8.GetString(Convert.FromBase64String(request.Authorization.Parameter!))
            .Should().Be($"{_options.OAuth2ClientId}:{_options.OAuth2ClientSecret}");
        request.Body.Should().Contain("grant_type=refresh_token")
            .And.Contain($"refresh_token={Uri.EscapeDataString(refreshToken)}");
    }

    [Fact(DisplayName =
        "The exchange posts the OAuth 1.0a token and secret as an RFC 8693 token exchange.")]
    public async Task exchange_posts_token_exchange_grant()
    {
        // Arrange
        var oauth1Token = _fixture.Create<string>();
        var oauth1Secret = _fixture.Create<string>();
        _x.Respond(HttpStatusCode.OK, TokenJson(_fixture.Create<string>(), _fixture.Create<string>(), 7200));
        var sut = _mocker.CreateInstance<XOAuth2TokenClient>();

        // Act
        var result = await sut.ExchangeOAuth1(oauth1Token, oauth1Secret);

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenOutcome.Success);
        result.Grant!.Source.Should().Be(XOAuth2TokenClient.ExchangeSource);
        _x.Requests.Single().Body.Should()
            .Contain(Uri.EscapeDataString("urn:ietf:params:oauth:grant-type:token-exchange").Replace("%20", "+"))
            .And.Contain($"subject_token={Uri.EscapeDataString(oauth1Token)}")
            .And.Contain(Uri.EscapeDataString("urn:x:params:oauth:token-type:oauth1_token"))
            .And.Contain($"oauth_token_secret={Uri.EscapeDataString(oauth1Secret)}");
    }

    [Theory(DisplayName = "Token endpoint failures are classified so callers know whether to retry, re-exchange or fix the client credentials.")]
    [InlineData(HttpStatusCode.BadRequest, "invalid_grant", XOAuth2TokenOutcome.Rejected)]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request", XOAuth2TokenOutcome.Rejected)]
    [InlineData(HttpStatusCode.BadRequest, "unauthorized_client", XOAuth2TokenOutcome.ClientCredentialsInvalid)]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_client", XOAuth2TokenOutcome.ClientCredentialsInvalid)]
    [InlineData(HttpStatusCode.TooManyRequests, null, XOAuth2TokenOutcome.Transient)]
    [InlineData(HttpStatusCode.ServiceUnavailable, null, XOAuth2TokenOutcome.Transient)]
    public async Task failures_are_classified(HttpStatusCode status, string? error, XOAuth2TokenOutcome expected)
    {
        // Arrange
        _x.Respond(status, error is null ? "{}" : $"{{\"error\":\"{error}\"}}");
        var sut = _mocker.CreateInstance<XOAuth2TokenClient>();

        // Act
        var result = await sut.Refresh(_fixture.Create<string>());

        // Assert
        result.Outcome.Should().Be(expected);
        result.Grant.Should().BeNull();
    }

    [Fact(DisplayName = "Without OAuth 2.0 client credentials nothing is sent to X.")]
    public async Task missing_client_credentials_sends_nothing()
    {
        // Arrange
        _options.OAuth2ClientSecret = null;
        var sut = _mocker.CreateInstance<XOAuth2TokenClient>();

        // Act
        var result = await sut.Refresh(_fixture.Create<string>());

        // Assert
        result.Outcome.Should().Be(XOAuth2TokenOutcome.ClientCredentialsInvalid);
        _x.Requests.Should().BeEmpty();
    }

    [Fact(DisplayName = "The token grant's ToString never includes token values, so logging a grant cannot leak it.")]
    public void grant_to_string_is_redacted()
    {
        // Arrange
        var grant = new RedditPodcastPoster.Persistence.Abstractions.Repositories.XOAuth2TokenGrant(
            _fixture.Create<string>(), _fixture.Create<string>(), _time.GetUtcNow().UtcDateTime, null,
            _time.GetUtcNow().UtcDateTime, XOAuth2TokenClient.RefreshSource);

        // Act
        var text = grant.ToString();

        // Assert
        text.Should().NotContain(grant.AccessToken).And.NotContain(grant.RefreshToken);
    }
}
