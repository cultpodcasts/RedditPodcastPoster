using AutoFixture;
using Azure;
using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Configuration.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class HourlyIndexingScheduleRules
{
    private const int IndexPasses = 4;
    private const int DaysToEnumerate = 8;
    private const int HoursPerDay = 24;

    private readonly Fixture _fixture = new();

    /// <summary>
    /// Midnight UTC on the soonest day (today or later) whose DayOfYear % 4 == 0, so expensive-YouTube
    /// rotation is deterministic without a fixed calendar literal.
    /// </summary>
    private static DateTime ScheduleOriginUtc
    {
        get
        {
            var today = DomainTestFixture.UtcToday;
            var daysToAdd = (4 - (today.DayOfYear % 4)) % 4;
            return today.AddDays(daysToAdd);
        }
    }

    private static IEnumerable<DateTime> EveryHourOfEightDays()
    {
        var origin = ScheduleOriginUtc;
        for (var day = 0; day < DaysToEnumerate; day++)
        {
            for (var hour = 0; hour < HoursPerDay; hour++)
            {
                yield return origin.AddDays(day).AddHours(hour);
            }
        }
    }

    private static IReadOnlyList<(DateTime Utc, HourlyIndexingSchedule.PassPlan Plan)> AllPlans()
    {
        return EveryHourOfEightDays()
            .SelectMany(utc => HourlyIndexingSchedule.For(utc).Select(plan => (utc, plan)))
            .ToList();
    }

    [Fact(DisplayName =
        "Every batch runs once every two hours: for each batch, 12 runs per UTC day across eight consecutive days.")]
    public void every_batch_runs_once_every_two_hours()
    {
        // Arrange
        var plans = AllPlans();

        // Act
        var runsPerBatchPerDay = Enumerable.Range(1, IndexPasses)
            .Select(batch => plans
                .Where(row => row.Plan.Pass == batch)
                .GroupBy(row => row.Utc.Date)
                .Select(g => g.Count())
                .ToArray())
            .ToArray();

        // Assert
        plans.Should().HaveCount(DaysToEnumerate * HoursPerDay * 2);
        foreach (var perDayCounts in runsPerBatchPerDay)
        {
            perDayCounts.Should().HaveCount(DaysToEnumerate);
            perDayCounts.Should().OnlyContain(count => count == 12);
        }
    }

    [Fact(DisplayName =
        "Every batch gets Spotify discovery on every run, because IndexSpotify is always true on the hour's schedule.")]
    public void every_batch_gets_spotify_discovery_on_every_run()
    {
        // Arrange / Act
        var plans = AllPlans();

        // Assert
        plans.Should().OnlyContain(row => row.Plan.IndexSpotify);
        Enumerable.Range(1, IndexPasses).Should().OnlyContain(batch =>
            plans.Any(row => row.Plan.Pass == batch && row.Plan.IndexSpotify));
    }

    [Fact(DisplayName =
        "Every batch is allowed expensive Spotify queries at least once every 24 hours.")]
    public void every_batch_gets_expensive_spotify_at_least_once_per_day()
    {
        // Arrange
        var plans = AllPlans();

        // Act
        var expensiveDaysMissing = Enumerable.Range(1, IndexPasses)
            .SelectMany(batch => plans
                .Where(row => row.Plan.Pass == batch)
                .GroupBy(row => row.Utc.Date)
                .Where(g => g.All(row => !row.Plan.ExpensiveSpotify))
                .Select(g => (batch, g.Key)))
            .ToArray();

        // Assert
        expensiveDaysMissing.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Every batch is allowed expensive YouTube queries at least once every 4 days, and only in an hour that resolves YouTube.")]
    public void every_batch_gets_expensive_youtube_within_four_days_only_when_resolving_youtube()
    {
        // Arrange
        var plans = AllPlans();

        // Act
        var expensiveYouTube = plans.Where(row => row.Plan.ExpensiveYouTube).ToArray();
        var batchesWithExpensive = expensiveYouTube.Select(row => row.Plan.Pass).Distinct().OrderBy(x => x).ToArray();
        var daysBetweenTurns = Enumerable.Range(1, IndexPasses)
            .Select(batch =>
            {
                var days = expensiveYouTube
                    .Where(row => row.Plan.Pass == batch)
                    .Select(row => row.Utc.Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToArray();
                return days.Length < 2
                    ? Array.Empty<int>()
                    : days.Zip(days.Skip(1), (a, b) => (b - a).Days).ToArray();
            })
            .ToArray();

        // Assert
        batchesWithExpensive.Should().Equal(1, 2, 3, 4);
        expensiveYouTube.Should().OnlyContain(row => row.Plan.ResolveYouTube);
        daysBetweenTurns.Should().OnlyContain(gaps => gaps.All(gap => gap == 4));
    }

    [Fact(DisplayName =
        "At most one pass per hour is allowed expensive Spotify queries, and at most one pass per day is allowed expensive YouTube queries.")]
    public void at_most_one_expensive_spotify_per_hour_and_one_expensive_youtube_per_day()
    {
        // Arrange
        var plans = AllPlans();

        // Act
        var spotifyPerHour = plans
            .GroupBy(row => row.Utc)
            .Select(g => g.Count(row => row.Plan.ExpensiveSpotify))
            .ToArray();
        var youtubePerDay = plans
            .GroupBy(row => row.Utc.Date)
            .Select(g => g.Count(row => row.Plan.ExpensiveYouTube))
            .ToArray();

        // Assert
        spotifyPerHour.Should().OnlyContain(count => count <= 1);
        youtubePerDay.Should().OnlyContain(count => count == 1);
    }

    [Fact(DisplayName =
        "The pass pairing is unchanged: even hours run passes 1 and 2, odd hours run passes 3 and 4.")]
    public void pass_pairing_is_unchanged()
    {
        // Arrange / Act
        var plans = AllPlans();

        // Assert
        plans.Where(row => row.Utc.Hour % 2 == 0)
            .Select(row => row.Plan.Pass)
            .Should().OnlyContain(pass => pass == 1 || pass == 2);
        plans.Where(row => row.Utc.Hour % 2 == 1)
            .Select(row => row.Plan.Pass)
            .Should().OnlyContain(pass => pass == 3 || pass == 4);
        plans.GroupBy(row => row.Utc).Should().OnlyContain(g => g.Count() == 2);
    }

    [Fact(DisplayName =
        "The Indexer activity builds its indexing context from the hour's schedule for its pass.")]
    public async Task indexer_builds_indexing_context_from_schedule_for_pass()
    {
        // Arrange — hour 6: pass 2 is the expensive-Spotify pass; pass 1 is not.
        var utc = ScheduleOriginUtc.AddHours(6);
        var operationIds = new[]
        {
            _fixture.Create<Guid>(), _fixture.Create<Guid>(), _fixture.Create<Guid>(), _fixture.Create<Guid>()
        };
        var indexIds = new[]
        {
            new[] { _fixture.Create<Guid>() },
            new[] { _fixture.Create<Guid>() },
            new[] { _fixture.Create<Guid>() },
            new[] { _fixture.Create<Guid>() }
        };
        var baseContext = new IndexerContext(operationIds, indexIds);

        IndexingContext? pass1Context = null;
        IndexingContext? pass2Context = null;

        async Task<IndexerContext> RunPass(int pass)
        {
            var mocker = new AutoMocker();
            mocker.Use(Options.Create(new IndexerOptions { ReleasedDaysAgo = 1 }));
            mocker.Use<IDateTimeService>(new FixedUtcDateTimeService(utc));
            mocker.Use<IIndexingStrategy>(new IndexingStrategy(mocker.Get<IDateTimeService>()));
            var reason = "enabled";
            mocker.GetMock<IActivityOptionsProvider>().Setup(x => x.RunIndex(out reason)).Returns(true);
            mocker.GetMock<IActivityMarshaller>()
                .Setup(x => x.Initiate(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(ActivityStatus.Initiated);
            mocker.GetMock<IActivityMarshaller>()
                .Setup(x => x.Complete(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(ActivityStatus.Completed);
            mocker.GetMock<IMemoryProbeOrchestrator>()
                .Setup(x => x.Start(It.IsAny<string>()))
                .Returns(mocker.GetMock<IMemoryProbeScope>().Object);
            mocker.GetMock<FunctionContext>()
                .SetupGet(x => x.InvocationId)
                .Returns(_fixture.Create<string>());
            mocker.GetMock<IPodcastsUpdater>()
                .Setup(x => x.UpdatePodcasts(It.IsAny<Guid[]>(), It.IsAny<IndexingContext>()))
                .Callback<Guid[], IndexingContext>((_, context) =>
                {
                    if (pass == 1)
                    {
                        pass1Context = context;
                    }
                    else
                    {
                        pass2Context = context;
                    }
                })
                .ReturnsAsync(true);
            mocker.GetMock<ICatalogueParentsUpdater>()
                .Setup(x => x.UpdateParents(It.IsAny<Guid[]>(), It.IsAny<IndexingContext>()))
                .ReturnsAsync(true);

            var wrapper = new IndexerContextWrapper(baseContext, pass);
            return await mocker.CreateInstance<Indexer.Activities.Indexer>()
                .Run(wrapper, mocker.GetMock<FunctionContext>().Object);
        }

        // Act
        await RunPass(1);
        await RunPass(2);

        // Assert
        pass1Context.Should().NotBeNull();
        pass2Context.Should().NotBeNull();
        pass1Context!.SkipExpensiveSpotifyQueries.Should().BeTrue();
        pass2Context!.SkipExpensiveSpotifyQueries.Should().BeFalse();
        pass1Context.IndexSpotify.Should().BeTrue();
        pass2Context.IndexSpotify.Should().BeTrue();
    }

    private sealed class FixedUtcDateTimeService(DateTime utcNow) : IDateTimeService
    {
        public DateTime GetUtcNow() => utcNow;

        public int GetHour() => utcNow.Hour;
    }
}
