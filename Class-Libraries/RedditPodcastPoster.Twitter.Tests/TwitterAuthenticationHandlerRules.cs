using System.Net;
using System.Text;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Twitter.Auth;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Tests.Fakes;

namespace RedditPodcastPoster.Twitter.Tests;

public class TwitterAuthenticationHandlerRules
{
    private static readonly Uri TweetsUri = new("https://api.x.com/2/tweets");
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly ScriptedHttpHandler _x = new();
    private readonly TwitterOptions _options;
    private readonly string _accessToken;
    private readonly string _refreshedToken;
    private readonly string _tweetJson;

    public TwitterAuthenticationHandlerRules()
    {
        _options = new TwitterOptions
        {
            ConsumerKey = _fixture.Create<string>(),
            ConsumerSecret = _fixture.Create<string>(),
            AccessToken = _fixture.Create<string>(),
            AccessTokenSecret = _fixture.Create<string>()
        };
        _accessToken = _fixture.Create<string>();
        _refreshedToken = _fixture.Create<string>();
        _tweetJson = $"{{\"text\":\"{_fixture.Create<string>()}\"}}";
        _mocker.Use(Options.Create(_options));
        _mocker.GetMock<IXOAuth2AccessTokenProvider>()
            .Setup(x => x.GetAccessToken(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_accessToken);
        _mocker.GetMock<IXOAuth2AccessTokenProvider>()
            .Setup(x => x.GetAccessToken(_accessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_refreshedToken);
    }

    private async Task<HttpResponseMessage> Send()
    {
        var handler = _mocker.CreateInstance<TwitterAuthenticationHandler>();
        handler.InnerHandler = _x;
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, TweetsUri)
        {
            Content = new StringContent(_tweetJson, Encoding.UTF8, "application/json")
        };
        return await invoker.SendAsync(request, CancellationToken.None);
    }

    [Fact(DisplayName =
        "Setting toggle: AuthMode defaults to OAuth1, so deploying this change leaves X signing exactly as before.")]
    public void auth_mode_defaults_to_oauth1()
    {
        // Arrange
        var options = new TwitterOptions();

        // Act
        var mode = options.AuthMode;

        // Assert
        mode.Should().Be(TwitterAuthMode.OAuth1);
        options.ApiBaseAddress.Should().Be(new Uri("https://api.x.com/"));
        options.RequestTimeoutSeconds.Should().Be(15);
    }

    [Fact(DisplayName =
        "Setting toggle: in OAuth1 mode requests carry an OAuth 1.0a signature and the OAuth 2.0 token store is never touched.")]
    public async Task oauth1_mode_signs_with_oauth1_and_never_asks_for_bearer()
    {
        // Arrange
        _options.AuthMode = TwitterAuthMode.OAuth1;

        // Act
        using var response = await Send();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _x.Requests.Should().ContainSingle();
        _x.Requests[0].Authorization!.Scheme.Should().Be("OAuth");
        _x.Requests[0].Authorization!.Parameter.Should().Contain("oauth_signature=")
            .And.Contain(Uri.EscapeDataString(_options.ConsumerKey!));
        _x.Requests[0].Body.Should().Be(_tweetJson);
        _mocker.GetMock<IXOAuth2AccessTokenProvider>()
            .Verify(x => x.GetAccessToken(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Setting toggle: in OAuth2 mode requests carry the OAuth 2.0 user-context bearer token.")]
    public async Task oauth2_mode_sends_bearer_token()
    {
        // Arrange
        _options.AuthMode = TwitterAuthMode.OAuth2;

        // Act
        using var response = await Send();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _x.Requests.Should().ContainSingle();
        _x.Requests[0].Authorization!.Scheme.Should().Be("Bearer");
        _x.Requests[0].Authorization!.Parameter.Should().Be(_accessToken);
    }

    [Fact(DisplayName =
        "A 401 in OAuth2 mode triggers exactly one refresh and one resend of the same request with the new token.")]
    public async Task unauthorized_triggers_exactly_one_refresh_and_resend()
    {
        // Arrange
        _options.AuthMode = TwitterAuthMode.OAuth2;
        _x.Respond(HttpStatusCode.Unauthorized).Respond(HttpStatusCode.Created);

        // Act
        using var response = await Send();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _x.Requests.Should().HaveCount(2);
        _x.Requests[1].Authorization!.Parameter.Should().Be(_refreshedToken);
        _x.Requests[1].Body.Should().Be(_tweetJson);
        _x.Requests[1].Uri.Should().Be(TweetsUri);
        _mocker.GetMock<IXOAuth2AccessTokenProvider>()
            .Verify(x => x.GetAccessToken(_accessToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A second 401 after the refresh is returned to the caller instead of refreshing again, so a broken grant cannot loop.")]
    public async Task second_unauthorized_is_not_retried()
    {
        // Arrange
        _options.AuthMode = TwitterAuthMode.OAuth2;
        _x.Respond(HttpStatusCode.Unauthorized).Respond(HttpStatusCode.Unauthorized);

        // Act
        using var response = await Send();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _x.Requests.Should().HaveCount(2);
        _mocker.GetMock<IXOAuth2AccessTokenProvider>()
            .Verify(x => x.GetAccessToken(It.IsNotNull<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When the refresh yields no new token the 401 is returned without resending.")]
    public async Task unauthorized_without_new_token_is_not_resent()
    {
        // Arrange
        _options.AuthMode = TwitterAuthMode.OAuth2;
        _mocker.GetMock<IXOAuth2AccessTokenProvider>()
            .Setup(x => x.GetAccessToken(_accessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?) null);
        _x.Respond(HttpStatusCode.Unauthorized);

        // Act
        using var response = await Send();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _x.Requests.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "With no OAuth 2.0 token available nothing is sent to X and the caller sees a 401 failure.")]
    public async Task no_token_sends_nothing()
    {
        // Arrange
        _options.AuthMode = TwitterAuthMode.OAuth2;
        _mocker.GetMock<IXOAuth2AccessTokenProvider>()
            .Setup(x => x.GetAccessToken(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?) null);

        // Act
        using var response = await Send();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _x.Requests.Should().BeEmpty();
    }
}
