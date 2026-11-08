using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq.AutoMock;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Auth;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Extensions;

namespace RedditPodcastPoster.Twitter.Tests;

public class TwitterServiceRegistrationRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly ServiceCollection _services = new();

    public TwitterServiceRegistrationRules()
    {
        _services.AddLogging();
        _services.AddSingleton(_mocker.GetMock<IXOAuth2TokenStore>().Object);
    }

    private ServiceProvider Build(Dictionary<string, string?> settings)
    {
        _services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        _services.AddXOAuth2TokenServices();
        return _services.BuildServiceProvider();
    }

    [Fact(DisplayName =
        "Setting toggle: twitter:AuthMode binds from configuration, and is OAuth1 when unset.")]
    public void auth_mode_binds_from_configuration()
    {
        // Arrange
        using var unset = Build(new Dictionary<string, string?>());

        // Act
        var mode = unset.GetRequiredService<Microsoft.Extensions.Options.IOptions<TwitterOptions>>().Value.AuthMode;

        // Assert
        mode.Should().Be(TwitterAuthMode.OAuth1);
    }

    [Fact(DisplayName = "Setting toggle: twitter:AuthMode=OAuth2 selects OAuth 2.0.")]
    public void auth_mode_oauth2_binds()
    {
        // Arrange
        using var provider = Build(new Dictionary<string, string?> {["twitter:AuthMode"] = "OAuth2"});

        // Act
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TwitterOptions>>().Value;

        // Assert
        options.AuthMode.Should().Be(TwitterAuthMode.OAuth2);
    }

    [Fact(DisplayName =
        "The exchange console's services resolve, so the one-off command cannot fail at startup on a missing registration.")]
    public void exchange_services_resolve()
    {
        // Arrange
        using var provider = Build(new Dictionary<string, string?>
        {
            ["twitter:OAuth2ClientId"] = _fixture.Create<string>()
        });

        // Act
        var exchanger = provider.GetRequiredService<IXOAuth2TokenExchanger>();
        var accessTokens = provider.GetRequiredService<IXOAuth2AccessTokenProvider>();

        // Assert
        exchanger.Should().BeOfType<XOAuth2TokenExchanger>();
        accessTokens.Should().BeOfType<XOAuth2AccessTokenProvider>();
    }

    [Fact(DisplayName =
        "The authorize console's services resolve, so the PKCE login cannot fail at startup on a missing registration.")]
    public void authorize_services_resolve()
    {
        // Arrange
        using var provider = Build(new Dictionary<string, string?>
        {
            ["twitter:OAuth2ClientId"] = _fixture.Create<string>()
        });

        // Act
        var authorizer = provider.GetRequiredService<IXOAuth2Authorizer>();

        // Assert
        authorizer.Should().BeOfType<XOAuth2Authorizer>();
    }
}
