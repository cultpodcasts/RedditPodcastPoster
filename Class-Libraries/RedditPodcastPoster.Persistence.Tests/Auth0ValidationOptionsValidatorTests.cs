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

    private static Auth0ValidationOptions ValidOptions() => new()
    {
        Audience = "https://api.example.com",
        Domain = "example.auth0.com",
        Issuer = "https://example.auth0.com/"
    };
}
