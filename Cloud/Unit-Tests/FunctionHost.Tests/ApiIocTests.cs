using Api;
using Api.Handlers;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests;

public class ApiIocTests
{
    public static IEnumerable<object[]> CanaryServices =>
    [
        [typeof(IDiscoveryCurationHandler)],
        [typeof(ISubmitUrlHandler)],
    ];

    [Theory]
    [MemberData(nameof(CanaryServices))]
    public async Task Api_composition_root_resolves_canary_service(Type serviceType)
    {
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        var act = async () => await FunctionHostTestSupport.ValidateCanaryServicesAsync(services, serviceType);

        await act.Should().NotThrowAsync();
    }
}
