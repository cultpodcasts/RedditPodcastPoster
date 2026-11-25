using AutoFixture;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Orchestrations;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Characterises the hourly stale-run guard before the function-based Durable migration: a late-draining
/// instance scheduled for an earlier UTC hour must no-op; legacy null-input runs stay unguarded.
/// </summary>
public class HourlyOrchestrationStaleRunRules
{
    private readonly Fixture _fixture = new();
    private readonly Mock<TaskOrchestrationContext> _context = new() { CallBase = true };
    private readonly List<string> _activities = [];
    private readonly DateTime _now = DateTime.UtcNow;

    public HourlyOrchestrationStaleRunRules()
    {
        _context.Protected().SetupGet<ILoggerFactory>("LoggerFactory").Returns(NullLoggerFactory.Instance);
        _context.SetupGet(x => x.InstanceId).Returns(_fixture.Create<string>());
        _context.SetupGet(x => x.CurrentUtcDateTime).Returns(_now);
        _context.Setup(x => x.NewGuid()).Returns(Guid.NewGuid);
        _context.Setup(x => x.CallActivityAsync<IndexIdProviderResponse>(
                It.IsAny<TaskName>(), It.IsAny<object?>(), It.IsAny<TaskOptions?>()))
            .ReturnsAsync(() =>
            {
                _activities.Add(nameof(IndexIdProvider));
                return new IndexIdProviderResponse(
                    Enumerable.Range(0, 4).Select(_ => new[] { _fixture.Create<Guid>() }).ToArray());
            });
        _context.Setup(x => x.CallActivityAsync<IndexerContext>(
                It.IsAny<TaskName>(), It.IsAny<object?>(), It.IsAny<TaskOptions?>()))
            .Returns((TaskName name, object? input, TaskOptions? _) =>
            {
                _activities.Add(name.Name);
                if (input is IndexerContextWrapper wrapper)
                {
                    return Task.FromResult(wrapper.IndexerContext with
                    {
                        Success = true,
                        SkipYouTubeUrlResolving = false,
                        YouTubeError = false,
                        SkipSpotifyUrlResolving = false,
                        SpotifyError = false
                    });
                }

                return Task.FromResult((IndexerContext)input!);
            });
    }

    [Fact(DisplayName =
        "When the hourly run was scheduled for an earlier UTC hour than CurrentUtcDateTime, RunAsync returns Success " +
        "without calling any activities, because a late-draining Pending backlog must not re-post.")]
    public async Task stale_scheduled_hour_skips_all_activities()
    {
        // Arrange
        var scheduledAtUtc = HourlyOrchestrationCatchUpEvaluator.GetUtcHourStart(_now).AddHours(-1).UtcDateTime;
        var input = new HourlyOrchestrationRunInput(scheduledAtUtc);
        var sut = new HourlyOrchestration();

        // Act
        var result = await sut.RunAsync(_context.Object, input);

        // Assert
        result.Success.Should().BeTrue();
        _activities.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When hourly input is null (legacy schedules), the orchestration still runs the pipeline unguarded, because " +
        "older triggers carry no ScheduledAtUtc and must not be treated as stale.")]
    public async Task null_input_still_runs_the_pipeline()
    {
        // Arrange
        var sut = new HourlyOrchestration();

        // Act
        var result = await sut.RunAsync(_context.Object, null);

        // Assert
        result.Should().NotBeNull();
        _activities.Should().Contain(nameof(IndexIdProvider));
        _activities.Should().Contain(nameof(LoadRecentCandidates));
    }
}
