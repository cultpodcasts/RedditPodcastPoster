using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Extensions;
using RedditPodcastPoster.Twitter.Validators;

namespace RedditPodcastPoster.Twitter.Tests;

public class TwitterOptionsValidatorRules
{
    private readonly Fixture _fixture = new();
    private readonly TwitterOptionsValidator _sut = new();

    private TwitterOptions CreateValidOAuth1Options() =>
        new()
        {
            AuthMode = TwitterAuthMode.OAuth1,
            ConsumerKey = _fixture.Create<string>(),
            ConsumerSecret = _fixture.Create<string>(),
            AccessToken = _fixture.Create<string>(),
            AccessTokenSecret = _fixture.Create<string>()
        };

    private TwitterOptions CreateValidOAuth2Options() =>
        new()
        {
            AuthMode = TwitterAuthMode.OAuth2,
            OAuth2ClientId = _fixture.Create<string>(),
            OAuth2ClientSecret = _fixture.Create<string>()
        };

    [Fact(DisplayName =
        "When AuthMode is OAuth1 and the four OAuth 1.0a credentials, absolute ApiBaseAddress, and positive RequestTimeoutSeconds are set, validation succeeds.")]
    public void oauth1_complete_options_succeed()
    {
        // Arrange
        var options = CreateValidOAuth1Options();

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When AuthMode is OAuth2 and OAuth2ClientId, OAuth2ClientSecret, absolute ApiBaseAddress, and positive RequestTimeoutSeconds are set, validation succeeds.")]
    public void oauth2_complete_options_succeed()
    {
        // Arrange
        var options = CreateValidOAuth2Options();

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Theory(DisplayName =
        "When AuthMode is OAuth1, a missing OAuth 1.0a credential fails validation with that config key.")]
    [InlineData(nameof(TwitterOptions.ConsumerKey))]
    [InlineData(nameof(TwitterOptions.ConsumerSecret))]
    [InlineData(nameof(TwitterOptions.AccessToken))]
    [InlineData(nameof(TwitterOptions.AccessTokenSecret))]
    public void oauth1_missing_credential_fails(string property)
    {
        // Arrange
        var options = CreateValidOAuth1Options();
        typeof(TwitterOptions).GetProperty(property)!.SetValue(options, null);

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains($"{TwitterOptions.SectionName}__{property}"));
    }

    [Theory(DisplayName =
        "When AuthMode is OAuth2, a missing OAuth 2.0 client credential fails validation with that config key.")]
    [InlineData(nameof(TwitterOptions.OAuth2ClientId))]
    [InlineData(nameof(TwitterOptions.OAuth2ClientSecret))]
    public void oauth2_missing_credential_fails(string property)
    {
        // Arrange
        var options = CreateValidOAuth2Options();
        typeof(TwitterOptions).GetProperty(property)!.SetValue(options, "   ");

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains($"{TwitterOptions.SectionName}__{property}"));
    }

    [Fact(DisplayName =
        "When AuthMode is OAuth1, missing OAuth 2.0 client credentials do not fail validation, because posting uses OAuth 1.0a keys.")]
    public void oauth1_does_not_require_oauth2_client()
    {
        // Arrange
        var options = CreateValidOAuth1Options();
        options.OAuth2ClientId = null;
        options.OAuth2ClientSecret = null;

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When AuthMode is OAuth2, missing OAuth 1.0a keys do not fail validation, because posting uses the OAuth 2.0 user token from LookUps.")]
    public void oauth2_does_not_require_oauth1_keys()
    {
        // Arrange
        var options = CreateValidOAuth2Options();
        options.ConsumerKey = null;
        options.ConsumerSecret = null;
        options.AccessToken = null;
        options.AccessTokenSecret = null;

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When AuthMode is neither OAuth1 nor OAuth2, validation fails with the AuthMode config key.")]
    public void invalid_auth_mode_fails()
    {
        // Arrange
        var options = CreateValidOAuth1Options();
        options.AuthMode = (TwitterAuthMode)42;

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f =>
            f.Contains($"{TwitterOptions.SectionName}__{nameof(TwitterOptions.AuthMode)}"));
    }

    [Fact(DisplayName =
        "When RequestTimeoutSeconds is not positive, validation fails with the RequestTimeoutSeconds config key.")]
    public void request_timeout_must_be_positive()
    {
        // Arrange
        var options = CreateValidOAuth1Options();
        options.RequestTimeoutSeconds = 0;

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f =>
            f.Contains($"{TwitterOptions.SectionName}__{nameof(TwitterOptions.RequestTimeoutSeconds)}"));
    }

    [Fact(DisplayName =
        "When ApiBaseAddress is a relative URI, validation fails with the ApiBaseAddress config key.")]
    public void api_base_address_must_be_absolute()
    {
        // Arrange
        var options = CreateValidOAuth1Options();
        options.ApiBaseAddress = new Uri("/relative", UriKind.Relative);

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f =>
            f.Contains($"{TwitterOptions.SectionName}__{nameof(TwitterOptions.ApiBaseAddress)}"));
    }

    [Fact(DisplayName =
        "AddTwitterServices registers TwitterOptions IValidateOptions and IStartupValidator; AddXOAuth2TokenServices alone does not register TwitterOptions validation.")]
    public void posting_registration_includes_validator_exchange_does_not()
    {
        // Arrange
        var posting = new ServiceCollection();
        posting.AddLogging();
        posting.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build());
        posting.AddTwitterServices();

        var exchange = new ServiceCollection();
        exchange.AddLogging();
        exchange.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build());
        exchange.AddXOAuth2TokenServices();

        // Act
        var postingHasValidator = posting.Any(d =>
            d.ServiceType == typeof(IValidateOptions<TwitterOptions>) &&
            d.ImplementationType == typeof(TwitterOptionsValidator));
        var postingHasStartupValidator = posting.Any(d => d.ServiceType == typeof(IStartupValidator));
        var exchangeHasValidator = exchange.Any(d =>
            d.ServiceType == typeof(IValidateOptions<TwitterOptions>));

        // Assert
        // AddHttpClient (used by AddXOAuth2TokenServices) also registers IStartupValidator, so absence of
        // that service type alone cannot prove exchange skipped Twitter ValidateOnStart — see the behavioural Fact.
        postingHasValidator.Should().BeTrue();
        postingHasStartupValidator.Should().BeTrue();
        exchangeHasValidator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When ValidateOnStart is enabled and AuthMode OAuth1 is missing ConsumerKey, IStartupValidator.Validate throws OptionsValidationException naming that config key.")]
    public void validate_on_start_rejects_incomplete_oauth1_options()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{TwitterOptions.SectionName}:ConsumerSecret"] = _fixture.Create<string>(),
                [$"{TwitterOptions.SectionName}:AccessToken"] = _fixture.Create<string>(),
                [$"{TwitterOptions.SectionName}:AccessTokenSecret"] = _fixture.Create<string>()
            }).Build());
        services.AddXOAuth2TokenServices();
        services.AddTwitterOptionsValidation(validateOnStart: true);

        using var provider = services.BuildServiceProvider();
        var startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        var act = () => startupValidator.Validate();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().Contain(f =>
                f.Contains($"{TwitterOptions.SectionName}__{nameof(TwitterOptions.ConsumerKey)}"));
    }

    [Fact(DisplayName =
        "When only AddXOAuth2TokenServices is registered with incomplete twitter credentials, IStartupValidator.Validate does not throw OptionsValidationException for twitter, because exchange consoles skip ValidateOnStart.")]
    public void exchange_registration_does_not_validate_twitter_options_on_start()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{TwitterOptions.SectionName}:ConsumerSecret"] = _fixture.Create<string>(),
                [$"{TwitterOptions.SectionName}:AccessToken"] = _fixture.Create<string>(),
                [$"{TwitterOptions.SectionName}:AccessTokenSecret"] = _fixture.Create<string>()
            }).Build());
        services.AddXOAuth2TokenServices();

        using var provider = services.BuildServiceProvider();
        var startupValidator = provider.GetService<IStartupValidator>();

        // Act
        var act = () => startupValidator?.Validate();

        // Assert
        act.Should().NotThrow<OptionsValidationException>();
    }

    [Fact(DisplayName =
        "When resolve-time validation is registered with AuthMode OAuth1 and complete credentials, resolving TwitterOptions succeeds.")]
    public void resolve_time_validation_accepts_valid_oauth1_options()
    {
        // Arrange
        var consumerKey = _fixture.Create<string>();
        var consumerSecret = _fixture.Create<string>();
        var accessToken = _fixture.Create<string>();
        var accessTokenSecret = _fixture.Create<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{TwitterOptions.SectionName}:ConsumerKey"] = consumerKey,
                [$"{TwitterOptions.SectionName}:ConsumerSecret"] = consumerSecret,
                [$"{TwitterOptions.SectionName}:AccessToken"] = accessToken,
                [$"{TwitterOptions.SectionName}:AccessTokenSecret"] = accessTokenSecret
            }).Build());
        services.AddXOAuth2TokenServices();
        services.AddTwitterOptionsValidation(validateOnStart: false);

        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => provider.GetRequiredService<IOptions<TwitterOptions>>().Value;

        // Assert
        var options = act.Should().NotThrow().Subject;
        options.AuthMode.Should().Be(TwitterAuthMode.OAuth1);
        options.ConsumerKey.Should().Be(consumerKey);
    }

    [Fact(DisplayName =
        "When resolve-time validation is registered with AuthMode OAuth1 and a missing ConsumerKey, resolving TwitterOptions throws.")]
    public void resolve_time_validation_rejects_incomplete_oauth1_options()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{TwitterOptions.SectionName}:ConsumerSecret"] = _fixture.Create<string>(),
                [$"{TwitterOptions.SectionName}:AccessToken"] = _fixture.Create<string>(),
                [$"{TwitterOptions.SectionName}:AccessTokenSecret"] = _fixture.Create<string>()
            }).Build());
        services.AddXOAuth2TokenServices();
        services.AddTwitterOptionsValidation(validateOnStart: false);

        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => _ = provider.GetRequiredService<IOptions<TwitterOptions>>().Value;

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().Contain(f =>
                f.Contains($"{TwitterOptions.SectionName}__{nameof(TwitterOptions.ConsumerKey)}"));
    }
}
