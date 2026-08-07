using Api;
using Api.Services.Episodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.ContentPublisher.Publishers;
using Xunit;

namespace FunctionHost.Tests.Api;

public class DiagnosticTimingDecoratorRegistrationTests
{
    [Fact(DisplayName =
        "Api IoC: when IHomepagePublisher is resolved, then TimedHomepagePublisher is returned wrapping HomepagePublisher, because diagnostic timing is an open-closed decorator.")]
    public async Task homepage_publisher_resolves_as_timed_decorator()
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // Act
        var publisher = provider.GetRequiredService<IHomepagePublisher>();
        var inner = provider.GetRequiredService<HomepagePublisher>();

        // Assert
        publisher.Should().BeOfType<TimedHomepagePublisher>();
        inner.Should().BeOfType<HomepagePublisher>();
        TimedHomepagePublisher.EnableDiagnosticTiming.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Api IoC: when IEpisodeUpdateService is resolved, then TimedEpisodeUpdateService is returned wrapping EpisodeUpdateService, because diagnostic timing is an open-closed decorator.")]
    public async Task episode_update_service_resolves_as_timed_decorator()
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // Act
        var service = provider.GetRequiredService<IEpisodeUpdateService>();
        var inner = provider.GetRequiredService<EpisodeUpdateService>();

        // Assert
        service.Should().BeOfType<TimedEpisodeUpdateService>();
        inner.Should().BeOfType<EpisodeUpdateService>();
        TimedEpisodeUpdateService.EnableDiagnosticTiming.Should().BeFalse();
    }
}
