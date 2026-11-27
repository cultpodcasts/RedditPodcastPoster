using System.Linq.Expressions;
using AutoFixture;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Orchestrations;
using Indexer.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Moq.Protected;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// #1560: the hour's two Indexer activities may run side by side behind <c>indexer__ParallelPasses</c>.
/// </summary>
public class HourlyParallelPassesRules
{
    private readonly Fixture _fixture = new();
    private readonly Mock<TaskOrchestrationContext> _context = new() { CallBase = true };
    private ParallelPassesMode _parallelMode = ParallelPassesMode.Off;
    private readonly List<string> _activities = [];
    private readonly Dictionary<string, IndexerContext> _activityInputs = new();
    private readonly List<(int Pass, IndexerContext Input)> _indexerCalls = [];
    private readonly HashSet<int> _failingPasses = [];
    private readonly List<TaskCompletionSource<IndexerContext>> _pendingIndexerCompletions = [];
    private bool _holdIndexerCompletions;
    private DateTime _now;

    public HourlyParallelPassesRules()
    {
        _now = UtcAtHour(5);
        _context.Protected().SetupGet<ILoggerFactory>("LoggerFactory").Returns(NullLoggerFactory.Instance);
        _context.SetupGet(x => x.InstanceId).Returns(_fixture.Create<string>());
        _context.SetupGet(x => x.CurrentUtcDateTime).Returns(() => _now);
        _context.Setup(x => x.NewGuid()).Returns(Guid.NewGuid);
        _context.Setup(x => x.CallActivityAsync<IndexIdProviderResponse>(
                It.IsAny<TaskName>(), It.IsAny<object?>(), It.IsAny<TaskOptions?>()))
            .ReturnsAsync(() =>
            {
                var podcastBatches = Enumerable.Range(0, 4)
                    .Select(_ => new[] { _fixture.Create<Guid>() })
                    .ToArray();
                var parentBatches = Enumerable.Range(0, 4)
                    .Select(_ => new[] { _fixture.Create<Guid>() })
                    .ToArray();
                return new IndexIdProviderResponse(podcastBatches, parentBatches, _parallelMode);
            });
        _context.Setup(x => x.CallActivityAsync<IndexerContext>(
                It.IsAny<TaskName>(), It.IsAny<object?>(), It.IsAny<TaskOptions?>()))
            .Returns((TaskName name, object? input, TaskOptions? _) => RunActivity(name.Name, input));
    }

    [Fact(DisplayName =
        "With parallel passes off the two Indexer passes run one after the other and the second receives the first's result.")]
    public async Task sequential_mode_chains_pass_outputs()
    {
        // Arrange
        _parallelMode = ParallelPassesMode.Off;
        _now = UtcAtHour(4); // even, non-YouTube

        // Act
        await HourlyOrchestration.Run(_context.Object, null);

        // Assert
        _indexerCalls.Should().HaveCount(2);
        _indexerCalls[0].Pass.Should().BeLessThan(_indexerCalls[1].Pass);
        ReferenceEquals(_indexerCalls[0].Input, _indexerCalls[1].Input).Should().BeFalse();
        _indexerCalls[1].Input.Success.Should().BeTrue(
            "the second pass must receive the first pass's returned context (Success already set), not the seed");
        _indexerCalls[1].Input.IndexerPassOperationIds.Should().BeSameAs(_indexerCalls[0].Input.IndexerPassOperationIds);
    }

    [Fact(DisplayName =
        "With parallel passes on both Indexer passes are scheduled before either is awaited and both receive the same input.")]
    public async Task parallel_mode_schedules_both_before_await()
    {
        // Arrange
        _parallelMode = ParallelPassesMode.All;
        _now = UtcAtHour(5); // odd, non-YouTube
        _holdIndexerCompletions = true;
        var run = HourlyOrchestration.Run(_context.Object, null);

        // Act
        await WaitUntilAsync(() => _pendingIndexerCompletions.Count == 2);
        var seed = _indexerCalls[0].Input;
        CompleteHeldIndexerPasses();
        await run;

        // Assert
        _indexerCalls.Should().HaveCount(2);
        _indexerCalls.Should().OnlyContain(c => ReferenceEquals(c.Input, seed),
            "both parallel passes must start from the same seed context");
        _activities.Should().Contain(nameof(Publisher));
    }

    public static TheoryData<int, bool> OddHoursModeHourRows()
    {
        var data = new TheoryData<int, bool>();
        for (var hour = 0; hour < 24; hour++)
        {
            data.Add(hour, hour % 2 == 1 && hour % 3 != 0);
        }

        return data;
    }

    [Theory(DisplayName =
        "In OddHours mode even hours run sequentially and odd hours run in parallel (YouTube-enabled hours stay sequential).")]
    [MemberData(nameof(OddHoursModeHourRows))]
    public void odd_hours_mode_follows_hour_parity(int hourUtc, bool expectParallel)
    {
        // Arrange
        // Act
        var (parallel, reason) = HourlyParallelPasses.Decide(ParallelPassesMode.OddHours, hourUtc);

        // Assert
        parallel.Should().Be(expectParallel);
        if (hourUtc % 2 == 1 && hourUtc % 3 == 0)
        {
            reason.Should().Be(HourlyParallelPasses.YouTubeHourReason);
        }
    }

    public static TheoryData<int, bool> AllModeHourRows()
    {
        var data = new TheoryData<int, bool>();
        for (var hour = 0; hour < 24; hour++)
        {
            data.Add(hour, hour % 3 != 0);
        }

        return data;
    }

    [Theory(DisplayName =
        "In All mode every non-YouTube hour runs in parallel, including even hours (Stage B soak).")]
    [MemberData(nameof(AllModeHourRows))]
    public void all_mode_parallels_non_youtube_hours_including_even(int hourUtc, bool expectParallel)
    {
        // Arrange
        // Act
        var (parallel, reason) = HourlyParallelPasses.Decide(ParallelPassesMode.All, hourUtc);

        // Assert
        parallel.Should().Be(expectParallel);
        if (hourUtc % 3 == 0)
        {
            reason.Should().Be(HourlyParallelPasses.YouTubeHourReason);
        }
        else
        {
            reason.Should().BeEmpty();
        }
    }

    [Fact(DisplayName =
        "Parallel passes never index the same podcast or catalogue parent.")]
    public void parallel_passes_never_share_podcast_or_parent_ids()
    {
        // Arrange
        var podcastIds = _fixture.CreateMany<Guid>(16).ToArray();
        var parentIds = _fixture.CreateMany<Guid>(12).ToArray();
        var podcastBatches = IndexPassBatchSplitter.Split(podcastIds, 4);
        var parentBatches = IndexPassBatchSplitter.Split(parentIds, 4);

        // Act — each hour runs one pair (1–2 or 3–4); every pair must be disjoint
        var pairs = new[] { (0, 1), (2, 3) };

        // Assert
        foreach (var (a, b) in pairs)
        {
            podcastBatches[a].Intersect(podcastBatches[b]).Should().BeEmpty();
            parentBatches[a].Intersect(parentBatches[b]).Should().BeEmpty();
        }

        podcastBatches.SelectMany(x => x).Should().BeEquivalentTo(podcastIds);
        parentBatches.SelectMany(x => x).Should().BeEquivalentTo(parentIds);
    }

    [Fact(DisplayName =
        "When one parallel pass fails the other pass's result is kept, posting is skipped and the orchestration ends Failed.")]
    public async Task one_parallel_pass_failure_keeps_peer_result_skips_posting_and_fails()
    {
        // Arrange
        _parallelMode = ParallelPassesMode.All;
        _now = UtcAtHour(5);
        var (firstPass, lastPass) = HourlyIndexingPassSelector.SelectPasses(_now.Hour);
        _failingPasses.Add(firstPass);

        // Act
        var act = () => HourlyOrchestration.Run(_context.Object, null);

        // Assert
        (await act.Should().ThrowAsync<IndexerPassesFailedException>())
            .Which.FailedPasses.Should().Equal(firstPass);
        _indexerCalls.Select(c => c.Pass).Should().BeEquivalentTo([firstPass, lastPass]);
        _activities.Should().NotContain([nameof(Poster), nameof(Tweet), nameof(Bluesky)]);
        _activities.Should().Contain(nameof(Publisher));
        _activityInputs[nameof(YouTubeThumbnailRecheck)].Should().Match<IndexerContext>(context =>
            context.YouTubeError == true && context.SpotifyError == true && context.Success == false);
    }

    [Fact(DisplayName =
        "The orchestrator takes the parallel mode from the IndexIdProvider result, not from configuration.")]
    public async Task orchestrator_uses_index_id_provider_parallel_mode()
    {
        // Arrange — activity returns All; the static orchestrator has no IOptions
        _parallelMode = ParallelPassesMode.All;
        _now = UtcAtHour(5);
        _holdIndexerCompletions = true;
        var run = HourlyOrchestration.Run(_context.Object, null);

        // Act
        await WaitUntilAsync(() => _pendingIndexerCompletions.Count == 2);
        CompleteHeldIndexerPasses();
        await run;

        // Assert — concurrent schedule of two Indexer activities proves the activity-supplied mode was used
        _indexerCalls.Should().HaveCount(2);
        ReferenceEquals(_indexerCalls[0].Input, _indexerCalls[1].Input).Should().BeTrue();
    }

    [Fact(DisplayName =
        "YouTube-enabled hours stay sequential with reason youtube-hour even when parallel mode is All, because absolute quota flush must not race.")]
    public async Task youtube_hours_stay_sequential_with_youtube_hour_reason()
    {
        // Arrange
        _parallelMode = ParallelPassesMode.All;
        _now = UtcAtHour(3); // hour % 3 == 0
        HourlyParallelPasses.Decide(ParallelPassesMode.All, _now.Hour)
            .Should().Be((false, HourlyParallelPasses.YouTubeHourReason));

        // Act
        await HourlyOrchestration.Run(_context.Object, null);

        // Assert — sequential chaining, not a shared seed
        _indexerCalls.Should().HaveCount(2);
        ReferenceEquals(_indexerCalls[0].Input, _indexerCalls[1].Input).Should().BeFalse();
        _indexerCalls[1].Input.Success.Should().BeTrue();
    }

    [Fact(DisplayName =
        "IndexIdProvider returns the ParallelPasses value from IndexerOptions so the orchestrator stays config-free.")]
    public async Task index_id_provider_returns_parallel_passes_from_options()
    {
        // Arrange
        var mocker = new AutoMocker();
        mocker.GetMock<FunctionContext>()
            .SetupGet(x => x.InvocationId)
            .Returns(_fixture.Create<string>());
        mocker.Use(Options.Create(new IndexerOptions { ParallelPasses = ParallelPassesMode.OddHours }));
        mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(AsyncEnumerable.Empty<Podcast>());
        mocker.GetMock<ICatalogueParentsUpdater>()
            .Setup(x => x.GetIndexableParentIds())
            .ReturnsAsync([]);
        var sut = mocker.CreateInstance<IndexIdProvider>();

        // Act
        var response = await sut.Run(new IndexIdProviderRequest(4), mocker.Get<FunctionContext>());

        // Assert
        response.ParallelPasses.Should().Be(ParallelPassesMode.OddHours);
    }

    private void CompleteHeldIndexerPasses()
    {
        for (var i = 0; i < _pendingIndexerCompletions.Count; i++)
        {
            _pendingIndexerCompletions[i].SetResult(_indexerCalls[i].Input with
            {
                Success = true,
                SkipYouTubeUrlResolving = false,
                YouTubeError = false,
                SkipSpotifyUrlResolving = false,
                SpotifyError = false
            });
        }
    }

    private Task<IndexerContext> RunActivity(string name, object? input)
    {
        _activities.Add(name);
        if (input is IndexerContextWrapper wrapper)
        {
            _indexerCalls.Add((wrapper.Pass, wrapper.IndexerContext));
            if (_failingPasses.Contains(wrapper.Pass))
            {
                // Faulted task (not a synchronous throw) so parallel schedule can CallActivityAsync both passes first.
                return Task.FromException<IndexerContext>(new TaskFailedException(
                    name,
                    _fixture.Create<int>(),
                    new TaskFailureDetails(nameof(TimeoutException), _fixture.Create<string>(), null, null, null)));
            }

            if (_holdIndexerCompletions)
            {
                var tcs = new TaskCompletionSource<IndexerContext>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingIndexerCompletions.Add(tcs);
                return tcs.Task;
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

    private static DateTime UtcAtHour(int hourUtc) =>
        DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(hourUtc), DateTimeKind.Utc);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met in time.");
    }
}
