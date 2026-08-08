using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Auth0;

public class ValidatedTokenToClientPrincipalTests
{
    [Fact(DisplayName =
        "Auth token mapping: when a validated token has claims, then ToClientPrincipal copies each type and value, because API authz reads ClientPrincipal claims.")]
    public void maps_all_claims()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("D");
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, subject),
            new Claim("permissions", "admin"),
            new Claim("permissions", "curate"),
            new Claim("scope", "openid profile admin curate")
        ]));
        var validated = new ValidatedToken(claimsPrincipal, new JwtSecurityToken());

        // Act
        var principal = validated.ToClientPrincipal();

        // Assert
        principal.Claims.Should().BeEquivalentTo(
        [
            new ClientPrincipalClaim { Type = ClaimTypes.NameIdentifier, Value = subject },
            new ClientPrincipalClaim { Type = "permissions", Value = "admin" },
            new ClientPrincipalClaim { Type = "permissions", Value = "curate" },
            new ClientPrincipalClaim { Type = "scope", Value = "openid profile admin curate" }
        ]);
        principal.HasScope("admin").Should().BeTrue();
        principal.Subject.Should().Be(subject);
    }

    [Fact(DisplayName =
        "Auth token mapping: when a validated token has no claims, then ToClientPrincipal yields an empty claim set, because nothing was asserted on the token.")]
    public void maps_empty_claims()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var validated = new ValidatedToken(claimsPrincipal, new JwtSecurityToken());

        // Act
        var principal = validated.ToClientPrincipal();

        // Assert
        principal.Claims.Should().BeEmpty();
        principal.HasScope("admin").Should().BeFalse();
        principal.Subject.Should().BeNull();
    }
}
