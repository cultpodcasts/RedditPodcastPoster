using Api;
using Api.Handlers;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests;

public class ApiIocTests
{
    public static IEnumerable<object[]> EntryPoints =>
    [
        [typeof(IPodcastHandler)],
        [typeof(IEpisodeHandler)],
        [typeof(IPublicHandler)],
        [typeof(IPublishHandler)],
        [typeof(IPushSubscriptionHandler)],
        [typeof(ISearchIndexHandler)],
        [typeof(ISubmitUrlHandler)],
        [typeof(ISubjectHandler)],
        [typeof(ITermsHandler)],
        [typeof(IDiscoveryCurationHandler)],
    ];

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public async Task Api_host_resolves_entry_point(Type entryPointType)
    {
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        var act = async () => await FunctionHostTestSupport.ValidateEntryPointAsync(services, entryPointType);

        await act.Should().NotThrowAsync();
    }
}
