using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Twitter.Auth;

namespace RedditPodcastPoster.Twitter.Tests;

public class XOAuth2PkceRules
{
    private readonly Fixture _fixture = new();
    [Fact(DisplayName =
        "The S256 challenge is the base64url SHA-256 of the verifier, so it matches the RFC 7636 vector and never equals the verifier.")]
    public void challenge_matches_the_rfc_vector()
    {
        // Arrange
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        // Act
        var challenge = XOAuth2Pkce.Challenge(verifier);

        // Assert
        challenge.Should().Be("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
        challenge.Should().NotBe(verifier);
    }

    [Fact(DisplayName =
        "A new pair uses an unreserved verifier of PKCE length, and the authorize URL carries the challenge and S256 but not the verifier.")]
    public void authorize_url_carries_the_challenge_only()
    {
        // Arrange
        var pair = XOAuth2Pkce.Create();
        var redirect = new Uri("http://127.0.0.1:8739/callback");
        var state = XOAuth2Pkce.NewState();

        // Act
        var url = XOAuth2Pkce.AuthorizeUrl(_fixture.Create<string>(), redirect, state, pair.Challenge,
            XOAuth2TokenExchanger.RequiredScopes);

        // Assert
        pair.Verifier.Length.Should().BeInRange(43, 128);
        pair.Challenge.Should().Be(XOAuth2Pkce.Challenge(pair.Verifier));
        url.AbsoluteUri.Should().StartWith(XOAuth2Pkce.AuthorizeEndpoint);
        url.Query.Should().Contain("code_challenge_method=S256")
            .And.Contain($"code_challenge={pair.Challenge}")
            .And.Contain("offline.access")
            .And.NotContain(pair.Verifier);
    }

    [Fact(DisplayName = "Only an http loopback URI with a port and no query is accepted as the redirect.")]
    public void redirect_must_be_loopback_http()
    {
        // Arrange
        var allowed = new Uri("http://127.0.0.1:8739/callback");
        var query = new Uri("http://127.0.0.1:8739/callback?x=1");
        var remote = new Uri("https://example.com/callback");

        // Act
        var allowedOk = XOAuth2Pkce.IsLoopbackRedirect(allowed);
        var queryOk = XOAuth2Pkce.IsLoopbackRedirect(query);
        var remoteOk = XOAuth2Pkce.IsLoopbackRedirect(remote);

        // Assert
        allowedOk.Should().BeTrue();
        queryOk.Should().BeFalse();
        remoteOk.Should().BeFalse();
    }
}
