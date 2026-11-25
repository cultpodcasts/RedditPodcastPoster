using Discovery;
using Discovery.Activities;
using Discovery.Models;
using Discovery.Orchestrations;
using Discovery.Services;
using Discovery.Triggers;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests;

public class DiscoveryIocTests
{
    /// <summary>
    /// Activities and triggers with constructor DI. Static orchestration (<see cref="Orchestration"/>) is a
    /// parameterless [Function] entry point and is not resolved from DI; its name is indexed into functions.metadata.
    /// </summary>
    public static IEnumerable<object[]> EntryPoints =>
    [
        [typeof(Discover)],
        [typeof(DiscoveryTrigger)],
    ];

    [Theory(DisplayName =
        "Discovery host: every activity and trigger resolves from Ioc.ConfigureServices, because a missing " +
        "registration fails the function at its first run.")]
    [MemberData(nameof(EntryPoints))]
    public async Task Discovery_host_resolves_entry_point(Type entryPointType)
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        // Act
        var act = async () => await FunctionHostTestSupport.ValidateEntryPointAsync(services, entryPointType);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "Discovery host: Orchestration keeps a stable function name for Durable scheduling, because " +
        "ScheduleNewOrchestrationInstanceAsync uses nameof and worker indexing must list it.")]
    public void Discovery_orchestration_keeps_stable_function_name()
    {
        // Arrange / Act / Assert
        nameof(Orchestration).Should().Be("Orchestration");
    }
}
