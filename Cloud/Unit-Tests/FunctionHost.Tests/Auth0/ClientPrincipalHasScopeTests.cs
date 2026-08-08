using System.Security.Claims;
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

    [Fact(DisplayName =
        "Auth scope check: when only the scp claim lists the scope, then HasScope is true, because Azure-style tokens use scp instead of scope.")]
    public void scp_claim_grants_scope()
    {
        // Arrange
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "scp", Value = "openid admin curate" }
            ]
        };

        // Act
        var hasAdmin = principal.HasScope("admin");
        var hasSubmit = principal.HasScope("submit");

        // Assert
        hasAdmin.Should().BeTrue();
        hasSubmit.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Auth scope check: when permissions and OAuth scope both exist, then permissions alone can grant access, because RBAC permissions are checked first.")]
    public void permissions_grant_even_when_oauth_scope_omits_permission()
    {
        // Arrange
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "permissions", Value = "admin" },
                new ClientPrincipalClaim { Type = "scope", Value = "openid profile" }
            ]
        };

        // Act
        var hasAdmin = principal.HasScope("admin");

        // Assert
        hasAdmin.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Auth scope check: when the OAuth scope claim is blank, then HasScope is false, because whitespace is not a grant.")]
    public void blank_oauth_scope_denies()
    {
        // Arrange
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "scope", Value = "   " }
            ]
        };

        // Act
        var hasAdmin = principal.HasScope("admin");

        // Assert
        hasAdmin.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Auth scope check: when no claims are present, then HasScope is false, because nothing authorises the caller.")]
    public void empty_claims_deny()
    {
        // Arrange
        var principal = new ClientPrincipal { Claims = [] };

        // Act
        var hasCurate = principal.HasScope("curate");

        // Assert
        hasCurate.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Auth scope check: when the OAuth scope contains a longer sibling token, then a shorter substring does not match, because grants are whole space-delimited tokens.")]
    public void oauth_scope_does_not_substring_match()
    {
        // Arrange
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "scope", Value = "openid curate" }
            ]
        };

        // Act
        var hasPartial = principal.HasScope("cur");

        // Assert
        hasPartial.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Auth subject: when a NameIdentifier claim is present, then Subject returns that value, because Easy Auth exposes the user id that way.")]
    public void subject_reads_name_identifier()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("D");
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = ClaimTypes.NameIdentifier, Value = subject }
            ]
        };

        // Act
        var result = principal.Subject;

        // Assert
        result.Should().Be(subject);
    }

    [Fact(DisplayName =
        "Auth subject: when NameIdentifier is absent, then Subject is null, because there is no authenticated subject claim.")]
    public void subject_null_without_name_identifier()
    {
        // Arrange
        var principal = new ClientPrincipal
        {
            Claims =
            [
                new ClientPrincipalClaim { Type = "permissions", Value = "admin" }
            ]
        };

        // Act
        var result = principal.Subject;

        // Assert
        result.Should().BeNull();
    }
}
