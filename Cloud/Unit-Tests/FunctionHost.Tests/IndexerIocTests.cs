using FluentAssertions;
using Indexer;
using Indexer.Activities;
using Indexer.Models;
using Indexer.Orchestrations;
using Indexer.Services;
using Indexer.Triggers;
using Xunit;

namespace FunctionHost.Tests;

public class IndexerIocTests
{
    /// <summary>
    /// Activities and triggers with constructor DI. Static orchestrations (<see cref="HourlyOrchestration"/>,
    /// <see cref="HalfHourlyOrchestration"/>) are parameterless [Function] entry points and are not resolved from DI;
    /// their names are indexed into functions.metadata for the worker.
    /// </summary>
    public static IEnumerable<object[]> EntryPoints =>
    [
        [typeof(global::Indexer.Activities.Indexer)],
        [typeof(Tweet)],
        [typeof(Poster)],
        [typeof(Categoriser)],
        [typeof(Bluesky)],
        [typeof(Publisher)],
        [typeof(LoadRecentCandidates)],
        [typeof(IndexIdProvider)],
        [typeof(YouTubeThumbnailRecheck)],
        [typeof(OrchestrationTrigger)],
        [typeof(D1SearchReconcileTrigger)],
    ];

    [Theory(DisplayName =
        "Indexer host: every activity and trigger resolves from Ioc.ConfigureServices, because a missing " +
        "registration fails the function at its first run.")]
    [MemberData(nameof(EntryPoints))]
    public async Task Indexer_host_resolves_entry_point(Type entryPointType)
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        // Act
        var act = async () => await FunctionHostTestSupport.ValidateEntryPointAsync(services, entryPointType);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "Indexer host: HourlyOrchestration and HalfHourlyOrchestration keep stable function names for Durable " +
        "scheduling, because ScheduleNewOrchestrationInstanceAsync uses nameof and worker indexing must list them.")]
    public void Indexer_orchestrations_keep_stable_function_names()
    {
        // Arrange / Act / Assert
        nameof(HourlyOrchestration).Should().Be("HourlyOrchestration");
        nameof(HalfHourlyOrchestration).Should().Be("HalfHourlyOrchestration");
    }
}
