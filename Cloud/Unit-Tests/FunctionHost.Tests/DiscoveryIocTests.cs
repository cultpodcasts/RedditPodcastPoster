using Discovery;
using FluentAssertions;
using Xunit;

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
