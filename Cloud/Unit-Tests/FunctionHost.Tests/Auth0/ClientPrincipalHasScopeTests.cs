using FluentAssertions;
using RedditPodcastPoster.Auth0.Models;
using Xunit;

namespace FunctionHost.Tests.Auth0;

public class ClientPrincipalHasScopeTests
{
    [Fact(DisplayName =
        "Auth scope check: when permissions claim lists the scope, then HasScope is true, because RBAC permissions are authoritative.")]
    public void permissions_claim_grants_scope()
    {
        // Arrange
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "permissions", Value = "admin" },
                new ClientPrincipalClaim { Type = "permissions", Value = "curate" }
            ]
        };

        // Act
        var hasCurate = principal.HasScope("curate");

        // Assert
        hasCurate.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Auth scope check: when only the OAuth scope string includes the scope, then HasScope is true, because Auth0 often omits the permissions array.")]
    public void oauth_scope_string_grants_scope()
    {
        // Arrange
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "scope", Value = "openid profile admin curate" }
            ]
        };

        // Act
        var hasCurate = principal.HasScope("curate");
        var hasAdmin = principal.HasScope("admin");
        var hasMissing = principal.HasScope("submit");

        // Assert
        hasCurate.Should().BeTrue();
        hasAdmin.Should().BeTrue();
        hasMissing.Should().BeFalse();
    }
}
