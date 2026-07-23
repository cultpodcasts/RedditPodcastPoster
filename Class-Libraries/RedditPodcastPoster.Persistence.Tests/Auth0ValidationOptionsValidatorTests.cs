using FluentAssertions;
using RedditPodcastPoster.Auth0.Configuration;
using RedditPodcastPoster.Auth0.Validators;

namespace RedditPodcastPoster.Persistence.Tests;

public class Auth0ValidationOptionsValidatorTests
{
    private readonly Auth0ValidationOptionsValidator _validator = new();

    [Fact]
    public void Succeeds_when_all_values_are_set()
    {
        _validator.Validate(null, ValidOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Succeeds_when_Staging_Trust_is_true_and_staging_is_configured()
    {
        var options = ValidOptionsWithTrustedStaging();

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Fails_when_Audience_is_blank()
    {
        var options = ValidOptions();
        options.Audience = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Audience");
    }

    [Fact]
    public void Fails_when_Domain_is_blank()
    {
        var options = ValidOptions();
        options.Domain = "";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Domain");
    }

    [Fact]
    public void Fails_when_Issuer_is_blank()
    {
        var options = ValidOptions();
        options.Issuer = "   ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Issuer");
    }

    [Fact]
    public void Fails_when_Staging_Trust_true_but_Domain_blank()
    {
        var options = ValidOptionsWithTrustedStaging();
        options.Staging!.Domain = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Staging");
        result.FailureMessage.Should().Contain("Domain");
    }

    [Fact]
    public void Fails_when_Staging_Trust_true_but_Issuer_blank()
    {
        var options = ValidOptionsWithTrustedStaging();
        options.Staging!.Issuer = null;

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Staging");
        result.FailureMessage.Should().Contain("Issuer");
    }

    [Fact]
    public void GetTrustedIssuers_excludes_staging_when_Trust_is_false()
    {
        var options = ValidOptionsWithTrustedStaging();
        options.Staging!.Trust = false;

        options.GetTrustedIssuers().Should().Equal(options.Issuer);
        options.GetTrustedDomains().Should().Equal(options.Domain);
        options.TrustsStagingIssuer.Should().BeFalse();
    }

    [Fact]
    public void GetTrustedIssuers_includes_staging_when_Trust_is_true()
    {
        var options = ValidOptionsWithTrustedStaging();

        options.GetTrustedIssuers().Should().Equal(options.Issuer, options.Staging!.Issuer);
        options.GetTrustedDomains().Should().Equal(options.Domain, options.Staging.Domain);
        options.TrustsStagingIssuer.Should().BeTrue();
    }

    private static Auth0ValidationOptions ValidOptions() => new()
    {
        Audience = "https://api.example.com",
        Domain = "example.auth0.com",
        Issuer = "https://example.auth0.com/"
    };

    private static Auth0ValidationOptions ValidOptionsWithTrustedStaging() => new()
    {
        Audience = "https://api.example.com",
        Domain = "example.auth0.com",
        Issuer = "https://example.auth0.com/",
        Staging = new Auth0StagingValidationOptions
        {
            Trust = true,
            Domain = "auth-staging.example.com",
            Issuer = "https://auth-staging.example.com/"
        }
    };
}
