using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Text.TitleCasing;
using Xunit;

namespace FunctionHost.Tests;

public class TitleCasingRulesWarmupRegistrationTests
{
    [Fact(DisplayName =
        "Title-casing warmup: when Api IoC is configured, then TitleCasingRulesStartupWarmer is registered, because Api SanitiseTitle on homepage/episode display must preload rules.")]
    public void Api_registers_title_casing_warmup()
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(global::Api.Ioc.ConfigureServices);

        // Act
        using var provider = services.BuildServiceProvider();
        var warmerNames = provider.GetServices<IStartupWarmer>().Select(w => w.Name).ToArray();

        // Assert
        warmerNames.Should().Contain(nameof(ITitleCasingRulesProvider));
    }

    [Fact(DisplayName =
        "Title-casing warmup: when Indexer IoC is configured, then TitleCasingRulesStartupWarmer is registered, because Indexer social posts SanitiseTitle.")]
    public void Indexer_registers_title_casing_warmup()
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(global::Indexer.Ioc.ConfigureServices);

        // Act
        using var provider = services.BuildServiceProvider();
        var warmerNames = provider.GetServices<IStartupWarmer>().Select(w => w.Name).ToArray();

        // Assert
        warmerNames.Should().Contain(nameof(ITitleCasingRulesProvider));
    }

    [Fact(DisplayName =
        "Title-casing warmup: when Discover IoC is configured, then TitleCasingRulesStartupWarmer is not registered, because Discover only ExtractDescription and must not preload title-casing.")]
    public void Discover_does_not_register_title_casing_warmup()
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(global::Discovery.Ioc.ConfigureServices);

        // Act
        using var provider = services.BuildServiceProvider();
        var warmerNames = provider.GetServices<IStartupWarmer>().Select(w => w.Name).ToArray();

        // Assert
        warmerNames.Should().NotContain(nameof(ITitleCasingRulesProvider));
        provider.GetService<IAsyncInstance<ITitleCasingRulesProvider>>().Should().NotBeNull(
            "Discover still registers the lazy provider via AddSubjectServices for TextSanitiser DI");
    }
}
