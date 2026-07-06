using FluentAssertions;
using Indexer;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;
using Xunit;

namespace FunctionHost.Tests;

public class IndexerIocTests
{
    public static IEnumerable<object[]> CanaryServices =>
    [
        [typeof(global::Indexer.Indexer)],
        [typeof(IPodcastsUpdater)],
    ];

    [Theory]
    [MemberData(nameof(CanaryServices))]
    public async Task Indexer_composition_root_resolves_canary_service(Type serviceType)
    {
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        var act = async () => await FunctionHostTestSupport.ValidateCanaryServicesAsync(services, serviceType);

        await act.Should().NotThrowAsync();
    }
}
