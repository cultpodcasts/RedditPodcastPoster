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
        [typeof(HourlyOrchestration)],
        [typeof(HalfHourlyOrchestration)],
        [typeof(OrchestrationTrigger)],
        [typeof(D1SearchReconcileTrigger)],
    ];

    [Theory(DisplayName =
        "Indexer host: every activity, orchestration and trigger resolves from Ioc.ConfigureServices, because a missing " +
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
}
