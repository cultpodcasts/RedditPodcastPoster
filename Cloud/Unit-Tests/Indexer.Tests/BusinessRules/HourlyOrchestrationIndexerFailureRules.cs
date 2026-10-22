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
/// An Indexer activity that fails (e.g. killed at the function timeout while one podcast hung) used to fail the whole
/// hourly orchestration, so the homepage was not published that hour. Posting after a failed pass is unchanged: Poster,
/// Tweet and Bluesky still do not run.
/// </summary>
public class HourlyOrchestrationIndexerFailureRules
{
    private readonly Fixture _fixture = new();
    private readonly Mock<TaskOrchestrationContext> _context = new() { CallBase = true };
    private readonly List<string> _activities = [];
    private readonly Dictionary<string, IndexerContext> _activityInputs = new();
    private readonly HashSet<int> _failingPasses = [];
    private readonly List<int> _passesRun = [];
    private readonly DateTime _now = DateTime.UtcNow;

    public HourlyOrchestrationIndexerFailureRules()
    {
        _context.Protected().SetupGet<ILoggerFactory>("LoggerFactory").Returns(NullLoggerFactory.Instance);
        _context.SetupGet(x => x.InstanceId).Returns(_fixture.Create<string>());
        _context.SetupGet(x => x.CurrentUtcDateTime).Returns(_now);
        _context.Setup(x => x.NewGuid()).Returns(Guid.NewGuid);
        _context.Setup(x => x.CallActivityAsync<IndexIdProviderResponse>(
                It.IsAny<TaskName>(), It.IsAny<object?>(), It.IsAny<TaskOptions?>()))
            .ReturnsAsync(() => new IndexIdProviderResponse(
                Enumerable.Range(0, 4).Select(_ => new[] { _fixture.Create<Guid>() }).ToArray()));
        _context.Setup(x => x.CallActivityAsync<IndexerContext>(
                It.IsAny<TaskName>(), It.IsAny<object?>(), It.IsAny<TaskOptions?>()))
            .Returns((TaskName name, object? input, TaskOptions? _) => RunActivity(name.Name, input));
    }

    [Fact(DisplayName =
        "When an Indexer pass fails, the hour still runs the remaining pass, Categoriser, the thumbnail re-check and " +
        "Publisher, because one stuck podcast must not cost the hour its homepage publish.")]
    public async Task failed_indexer_pass_still_publishes()
    {
        // Arrange
        var sut = new HourlyOrchestration();
        var (firstPass, lastPass) = HourlyIndexingPassSelector.SelectPasses(_now.Hour);
        _failingPasses.Add(firstPass);

        // Act
        var act = () => sut.RunAsync(_context.Object, null);

        // Assert
        await act.Should().ThrowAsync<IndexerPassesFailedException>();
        _passesRun.Should().Equal(firstPass, lastPass);
        _activities.Should().ContainInOrder(
            nameof(LoadRecentCandidates), nameof(Categoriser), nameof(YouTubeThumbnailRecheck), nameof(Publisher));
    }

    [Fact(DisplayName =
        "After a failed Indexer pass, Poster (Reddit), Tweet and Bluesky do not run at all, because posting after an " +
        "indexer failure was never approved and must stay exactly as before.")]
    public async Task failed_indexer_pass_skips_all_posting()
    {
        // Arrange
        var sut = new HourlyOrchestration();
        var (firstPass, _) = HourlyIndexingPassSelector.SelectPasses(_now.Hour);
        _failingPasses.Add(firstPass);

        // Act
        var act = () => sut.RunAsync(_context.Object, null);

        // Assert
        await act.Should().ThrowAsync<IndexerPassesFailedException>();
        _activities.Should().NotContain([nameof(Poster), nameof(Tweet), nameof(Bluesky)]);
    }

    [Fact(DisplayName =
        "After a failed Indexer pass, the thumbnail re-check sees YouTube and Spotify as errored, because it must not " +
        "make YouTube calls on an hour whose index state is unknown.")]
    public async Task failed_indexer_pass_flags_youtube_errored_for_the_recheck()
    {
        // Arrange
        var sut = new HourlyOrchestration();
        var (firstPass, _) = HourlyIndexingPassSelector.SelectPasses(_now.Hour);
        _failingPasses.Add(firstPass);

        // Act
        var act = () => sut.RunAsync(_context.Object, null);

        // Assert
        await act.Should().ThrowAsync<IndexerPassesFailedException>();
        _activityInputs[nameof(YouTubeThumbnailRecheck)].Should().Match<IndexerContext>(context =>
            context.YouTubeError == true && context.SpotifyError == true && context.Success == false);
    }

    [Fact(DisplayName =
        "After a failed Indexer pass the orchestration still ends Failed, naming the failed pass, because the hourly " +
        "health check reports Failed instances and the failure must stay visible.")]
    public async Task failed_indexer_pass_still_fails_the_instance()
    {
        // Arrange
        var sut = new HourlyOrchestration();
        var (firstPass, _) = HourlyIndexingPassSelector.SelectPasses(_now.Hour);
        _failingPasses.Add(firstPass);

        // Act
        var act = () => sut.RunAsync(_context.Object, null);

        // Assert
        (await act.Should().ThrowAsync<IndexerPassesFailedException>())
            .Which.FailedPasses.Should().Equal(firstPass);
    }

    [Fact(DisplayName =
        "When every Indexer pass succeeds, the orchestration completes normally and Poster, Tweet and Bluesky run " +
        "with posts not held back.")]
    public async Task successful_passes_complete_normally()
    {
        // Arrange
        var sut = new HourlyOrchestration();

        // Act
        var result = await sut.RunAsync(_context.Object, null);

        // Assert
        result.Should().NotBeNull();
        _activities.Should().ContainInOrder(nameof(Poster), nameof(Publisher), nameof(Tweet), nameof(Bluesky));
        _activityInputs[nameof(Tweet)].YouTubeError.Should().BeFalse();
        _activityInputs[nameof(Tweet)].SpotifyError.Should().BeFalse();
    }

    private Task<IndexerContext> RunActivity(string name, object? input)
    {
        _activities.Add(name);
        if (input is IndexerContextWrapper wrapper)
        {
            _passesRun.Add(wrapper.Pass);
            if (_failingPasses.Contains(wrapper.Pass))
            {
                throw new TaskFailedException(
                    name,
                    _fixture.Create<int>(),
                    new TaskFailureDetails(nameof(TimeoutException), _fixture.Create<string>(), null, null, null));
            }

            return Task.FromResult(wrapper.IndexerContext with
            {
                Success = true,
                SkipYouTubeUrlResolving = false,
                YouTubeError = false,
                SkipSpotifyUrlResolving = false,
                SpotifyError = false
            });
        }

        var context = (IndexerContext) input!;
        _activityInputs[name] = context;
        return Task.FromResult(context);
    }
}
