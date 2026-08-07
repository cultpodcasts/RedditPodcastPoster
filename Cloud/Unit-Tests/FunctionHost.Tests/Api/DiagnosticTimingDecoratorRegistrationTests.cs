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
        "Api IoC: when TimedHomepagePublisher.EnableDiagnosticTiming is false, then IHomepagePublisher resolves as HomepagePublisher (no decorator), because timing wrap is IoC-switched by const.")]
    public async Task homepage_publisher_resolves_plain_when_timing_disabled()
    {
        // Arrange
        TimedHomepagePublisher.EnableDiagnosticTiming.Should().BeFalse(
            "this test documents the default-off IoC path; flip the const and update asserts when enabling timing");
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // Act
        var publisher = provider.GetRequiredService<IHomepagePublisher>();
        var concrete = provider.GetRequiredService<HomepagePublisher>();

        // Assert
        publisher.Should().BeSameAs(concrete);
        publisher.Should().BeOfType<HomepagePublisher>();
    }

    [Fact(DisplayName =
        "Api IoC: when TimedEpisodeUpdateService.EnableDiagnosticTiming is false, then IEpisodeUpdateService resolves as EpisodeUpdateService (no decorator), because timing wrap is IoC-switched by const.")]
    public async Task episode_update_service_resolves_plain_when_timing_disabled()
    {
        // Arrange
        TimedEpisodeUpdateService.EnableDiagnosticTiming.Should().BeFalse(
            "this test documents the default-off IoC path; flip the const and update asserts when enabling timing");
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // Act
        var service = provider.GetRequiredService<IEpisodeUpdateService>();
        var concrete = provider.GetRequiredService<EpisodeUpdateService>();

        // Assert
        service.Should().BeSameAs(concrete);
        service.Should().BeOfType<EpisodeUpdateService>();
    }
}
