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
/// Characterises the half-hourly posting pipeline order and held-back YouTube/Spotify flags before the
/// function-based Durable migration.
/// </summary>
public class HalfHourlyOrchestrationPipelineRules
{
    private readonly Fixture _fixture = new();
    private readonly Mock<TaskOrchestrationContext> _context = new() { CallBase = true };
    private readonly List<string> _activities = [];
    private readonly Dictionary<string, IndexerContext> _activityInputs = new();

    public HalfHourlyOrchestrationPipelineRules()
    {
        _context.Protected().SetupGet<ILoggerFactory>("LoggerFactory").Returns(NullLoggerFactory.Instance);
        _context.SetupGet(x => x.InstanceId).Returns(_fixture.Create<string>());
        _context.SetupGet(x => x.CurrentUtcDateTime).Returns(DateTime.UtcNow);
        _context.Setup(x => x.NewGuid()).Returns(Guid.NewGuid);
        _context.Setup(x => x.CallActivityAsync<IndexerContext>(
                It.IsAny<TaskName>(), It.IsAny<object?>(), It.IsAny<TaskOptions?>()))
            .Returns((TaskName name, object? input, TaskOptions? _) =>
            {
                _activities.Add(name.Name);
                var context = (IndexerContext)input!;
                _activityInputs[name.Name] = context;
                return Task.FromResult(context);
            });
    }

    [Fact(DisplayName =
        "Half-hourly RunAsync calls LoadRecentCandidates, then Poster, then Publisher, then Bluesky, because that is " +
        "the posting-only pipeline without Indexer or Tweet.")]
    public async Task activity_order_is_load_poster_publisher_bluesky()
    {
        // Arrange
        var sut = new HalfHourlyOrchestration();

        // Act
        await sut.RunAsync(_context.Object, new object());

        // Assert
        _activities.Should().Equal(
            nameof(LoadRecentCandidates),
            nameof(Poster),
            nameof(Publisher),
            nameof(Bluesky));
    }

    [Fact(DisplayName =
        "Half-hourly activities receive an IndexerContext with YouTube and Spotify held back (Skip* and *Error true), " +
        "because the half-hour run must not post items that still need URL resolving.")]
    public async Task activities_receive_youtube_and_spotify_held_back()
    {
        // Arrange
        var sut = new HalfHourlyOrchestration();

        // Act
        await sut.RunAsync(_context.Object, new object());

        // Assert
        foreach (var activityName in new[]
                 {
                     nameof(LoadRecentCandidates), nameof(Poster), nameof(Publisher), nameof(Bluesky)
                 })
        {
            _activityInputs[activityName].Should().Match<IndexerContext>(context =>
                context.SkipYouTubeUrlResolving == true &&
                context.YouTubeError == true &&
                context.SkipSpotifyUrlResolving == true &&
                context.SpotifyError == true);
        }
    }
}
