using Discovery;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Persistence.Abstractions;
using Xunit;

namespace FunctionHost.Tests;

public class DiscoveryIocTests
{
    public static IEnumerable<object[]> CanaryServices =>
    [
        [typeof(Discover)],
        [typeof(IDiscoveryService)],
        [typeof(IDiscoveryResultsRepository)],
    ];

    [Theory]
    [MemberData(nameof(CanaryServices))]
    public async Task Discovery_composition_root_resolves_canary_service(Type serviceType)
    {
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        var act = async () => await FunctionHostTestSupport.ValidateCanaryServicesAsync(services, serviceType);

        await act.Should().NotThrowAsync();
    }
}
