using FluentAssertions;
using Indexer;
using Xunit;
using Indexer.Activities;
using Indexer.Orchestrations;
using Indexer.Triggers;
using Indexer.Services;
using Indexer.Models;

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
    ];

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public async Task Indexer_host_resolves_entry_point(Type entryPointType)
    {
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        var act = async () => await FunctionHostTestSupport.ValidateEntryPointAsync(services, entryPointType);

        await act.Should().NotThrowAsync();
    }
}
