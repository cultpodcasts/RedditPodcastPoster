using Discovery;
using FluentAssertions;
using Xunit;
using Discovery.Activities;
using Discovery.Orchestrations;
using Discovery.Triggers;
using Discovery.Services;
using Discovery.Models;

namespace FunctionHost.Tests;

public class DiscoveryIocTests
{
    public static IEnumerable<object[]> EntryPoints =>
    [
        [typeof(Discover)],
        [typeof(Orchestration)],
        [typeof(DiscoveryTrigger)],
    ];

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public async Task Discovery_host_resolves_entry_point(Type entryPointType)
    {
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        var act = async () => await FunctionHostTestSupport.ValidateEntryPointAsync(services, entryPointType);

        await act.Should().NotThrowAsync();
    }
}
