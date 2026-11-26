using AutoFixture;
using Discovery.Activities;
using Discovery.Models;
using Discovery.Orchestrations;
using FluentAssertions;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Discovery.Tests.BusinessRules;

/// <summary>
/// Characterises Discovery Orchestration stale-run, success, and incomplete paths before the
/// function-based Durable migration.
/// </summary>
public class DiscoveryOrchestrationRules
{
    private static readonly string[] RunTimesUk = ["08:00", "22:00"];

    private readonly Fixture _fixture = new();
    private readonly Mock<TaskOrchestrationContext> _context = new() { CallBase = true };
    private readonly List<string> _activities = [];
    private DiscoveryContext? _discoverResult;
    private DateTime _currentUtc = DateTime.UtcNow;

    public DiscoveryOrchestrationRules()
    {
        _context.Protected().SetupGet<ILoggerFactory>("LoggerFactory").Returns(NullLoggerFactory.Instance);
        _context.SetupGet(x => x.InstanceId).Returns(_fixture.Create<string>());
        _context.SetupGet(x => x.CurrentUtcDateTime).Returns(() => _currentUtc);
        _context.Setup(x => x.NewGuid()).Returns(() => _fixture.Create<Guid>());
        _context.Setup(x => x.CallActivityAsync<DiscoveryContext>(
                It.IsAny<TaskName>(), It.IsAny<object?>(), It.IsAny<TaskOptions?>()))
            .Returns((TaskName name, object? input, TaskOptions? _) =>
            {
                _activities.Add(name.Name);
                return Task.FromResult(_discoverResult ?? (DiscoveryContext)input!);
            });
    }

    [Fact(DisplayName =
        "When DiscoverySchedule.IsStaleRun is true for the input slot versus CurrentUtcDateTime, Orchestration returns " +
        "Success without calling Discover, because a late-draining prior slot must not re-run discovery.")]
    public async Task stale_slot_skips_discover()
    {
        // Arrange
        var runTimes = DiscoverySchedule.ParseRunTimes(RunTimesUk);
        var currentSlot = DiscoverySchedule.GetLatestDueSlot(_currentUtc, runTimes);
        var priorSlot = DiscoverySchedule.GetPriorSlot(currentSlot, runTimes);
        DiscoverySchedule.IsStaleRun(priorSlot.SlotStartUtc, _currentUtc, runTimes).Should().BeTrue();
        var input = new DiscoveryOrchestrationRunInput(
            ScheduledAtUtc: priorSlot.SlotStartUtc.UtcDateTime,
            SlotStartUtc: priorSlot.SlotStartUtc,
            SlotId: priorSlot.SlotId,
            RunTimesUk: RunTimesUk);
        // Act
        var result = await Orchestration.Run(_context.Object, input);

        // Assert
        result.Success.Should().BeTrue();
        result.DiscoveryOperationId.Should().Be(Guid.Empty);
        _activities.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When Discover succeeds, Orchestration calls Discover once and returns that success context.")]
    public async Task success_path_calls_discover_once()
    {
        // Arrange
        var operationId = _fixture.Create<Guid>();
        _discoverResult = new DiscoveryContext(operationId, Success: true);
        // Act
        var result = await Orchestration.Run(_context.Object, null);

        // Assert
        result.Should().BeEquivalentTo(_discoverResult);
        _activities.Should().Equal(nameof(Discover));
    }

    [Fact(DisplayName =
        "When Discover returns non-success and is not a duplicate discovery operation, Orchestration throws " +
        "DiscoveryOrchestrationIncompleteException, because a failed discovery must fail the instance.")]
    public async Task non_success_without_duplicate_throws_incomplete()
    {
        // Arrange
        _discoverResult = new DiscoveryContext(_fixture.Create<Guid>(), Success: false, DuplicateDiscoveryOperation: false);
        // Act
        var act = () => Orchestration.Run(_context.Object, null);

        // Assert
        await act.Should().ThrowAsync<DiscoveryOrchestrationIncompleteException>();
        _activities.Should().Equal(nameof(Discover));
    }
}
