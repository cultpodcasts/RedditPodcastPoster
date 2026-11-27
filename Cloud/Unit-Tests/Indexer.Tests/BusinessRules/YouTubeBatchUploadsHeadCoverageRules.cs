using FluentAssertions;
using Indexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Batches that are not on today's expensive-YouTube turn still plan an uploads-head check on
/// every YouTube-enabled hour, which is what discovers a video appended to an oldest-first playlist.
/// </summary>
public class YouTubeBatchUploadsHeadCoverageRules
{
    private const int IndexPasses = 4;

    private readonly DomainTestFixture _fixture = new();
    private readonly YouTubeIndexingHintsOptions _options = new();
    private readonly DateTime _now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(12));
    private readonly Podcast _podcast;

    /// <summary>Midnight on a DayOfYear % 4 == 0 day so only pass 1 gets expensive YouTube (at hour 0).</summary>
    private static DateTime DayMod0
    {
        get
        {
            var today = DomainTestFixture.UtcToday;
            var daysToAdd = (4 - (today.DayOfYear % 4)) % 4;
            return today.AddDays(daysToAdd);
        }
    }

    public YouTubeBatchUploadsHeadCoverageRules()
    {
        _podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        _podcast.YouTubeIndexingHints = new YouTubeIndexingHints
        {
            PlaylistId = _podcast.YouTubePlaylistId,
            LastFullWalk = _now.AddHours(-3),
            PlaylistItemCount = _options.UploadsHeadMinimumPlaylistItems + 1
        };
    }

    [Theory(DisplayName =
        "Every indexer batch plans an uploads-head check on each of its YouTube-enabled hours, including hours " +
        "where expensive YouTube queries are skipped, because a fresh large playlist is read from the channel " +
        "uploads head and that check is what finds a video appended to the tail.")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void every_batch_plans_uploads_head_on_its_youtube_hours(int pass)
    {
        // Arrange
        var youTubeHours = YouTubeHoursForPass(pass);
        var expensiveAllowedHours = youTubeHours.Where(hour => !SkipExpensiveYouTube(pass, hour)).ToArray();

        // Act
        var plans = youTubeHours.Select(hour =>
        {
            var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2),
                SkipExpensiveYouTubeQueries: SkipExpensiveYouTube(pass, hour))
            {
                UseYouTubeIndexingHints = true
            };
            return YouTubeIndexingHintsPlanner.Plan(_podcast, context, _options, _now);
        }).ToArray();

        // Assert
        youTubeHours.Should().NotBeEmpty();
        plans.Should().OnlyContain(plan =>
            plan.Plan == YouTubePlaylistDiscoveryPlan.UploadsHead &&
            plan.Reason == YouTubeIndexingHintsPlanner.HintsUsable);
        if (pass == 1)
        {
            expensiveAllowedHours.Should().Equal(0);
        }
        else
        {
            expensiveAllowedHours.Should().BeEmpty();
        }
    }

    private static int[] YouTubeHoursForPass(int pass)
    {
        return Enumerable.Range(0, 24)
            .Where(hour =>
            {
                var utc = DayMod0.AddHours(hour);
                var plan = HourlyIndexingSchedule.ForPass(utc, pass);
                return plan is { ResolveYouTube: true };
            })
            .ToArray();
    }

    /// <summary>
    /// Same combination as <c>Indexer.Run</c>: expensive YouTube queries only when the hour's
    /// <see cref="HourlyIndexingSchedule.PassPlan.ExpensiveYouTube"/> is set for that pass.
    /// </summary>
    private static bool SkipExpensiveYouTube(int pass, int hour)
    {
        var utc = DayMod0.AddHours(hour);
        var plan = HourlyIndexingSchedule.ForPass(utc, pass);
        return plan is null || !plan.ExpensiveYouTube;
    }
}
