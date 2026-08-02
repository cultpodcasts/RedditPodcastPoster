using FluentAssertions;
using Indexer.Orchestrations;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Services;
using Xunit;

namespace Indexer.Tests;

public class YouTubeIndexingDaySpreadRules
{
    private const int IndexPasses = 4;

    [Theory(DisplayName =
        "A podcast in a given indexer pass receives YouTube resolving on exactly four UTC hours per day at the expected slots for its batch half.")]
    [InlineData(1, new[] { 0, 6, 12, 18 })]
    [InlineData(2, new[] { 0, 6, 12, 18 })]
    [InlineData(3, new[] { 3, 9, 15, 21 })]
    [InlineData(4, new[] { 3, 9, 15, 21 })]
    public void youtube_enabled_hours_for_pass_match_expected_day_spread(int pass, int[] expectedHours)
    {
        // Arrange / Act
        var youTubeHours = YouTubeEnabledHoursForPass(pass);

        // Assert
        youTubeHours.Should().Equal(expectedHours);
    }

    [Fact(DisplayName =
        "Every indexer pass is covered by at least one YouTube-enabled hour so any podcast placed in any IndexIdProvider batch still gets YouTube discovery that day.")]
    public void every_pass_has_youtube_enabled_hours()
    {
        // Arrange / Act
        var passesWithoutYouTube = Enumerable.Range(1, IndexPasses)
            .Where(pass => YouTubeEnabledHoursForPass(pass).Length == 0)
            .ToArray();

        // Assert
        passesWithoutYouTube.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "Consecutive YouTube-enabled slots for a pass are at most six hours apart (including midnight wrap) so discovery latency stays bounded under the every-three-hours cadence.")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void youtube_enabled_hour_gaps_for_pass_are_at_most_six_hours(int pass)
    {
        // Arrange
        var hours = YouTubeEnabledHoursForPass(pass);

        // Act
        var gaps = Enumerable.Range(0, hours.Length)
            .Select(i =>
            {
                var current = hours[i];
                var next = hours[(i + 1) % hours.Length];
                return next > current ? next - current : next + 24 - current;
            })
            .ToArray();

        // Assert
        hours.Should().NotBeEmpty();
        gaps.Should().OnlyContain(gap => gap > 0 && gap <= 6);
    }

    [Fact(DisplayName =
        "Index pass batching places every podcast id in exactly one pass batch, so schedule coverage of passes 1–4 covers the full indexable catalogue.")]
    public void index_pass_batching_assigns_every_podcast_to_exactly_one_pass()
    {
        // Arrange — catalogue size need not divide evenly; remainder joins the last batch
        var podcastIds = Enumerable.Range(0, 87).Select(_ => Guid.NewGuid()).ToArray();

        // Act
        var batches = IndexPassBatchSplitter.Split(podcastIds, IndexPasses);

        // Assert
        batches.Should().HaveCount(IndexPasses);
        batches.SelectMany(batch => batch).Should().BeEquivalentTo(podcastIds);
        batches.SelectMany(batch => batch).Should().OnlyHaveUniqueItems();
        batches.Should().OnlyContain(batch => batch.Length > 0);
    }

    [Fact(DisplayName =
        "For every podcast id after batching, the YouTube-enabled hours for its pass match the lower-half or upper-half day spread so no podcast is left without YouTube discovery windows.")]
    public void every_batched_podcast_receives_expected_youtube_day_spread_for_its_pass()
    {
        // Arrange
        var podcastIds = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToArray();
        var batches = IndexPassBatchSplitter.Split(podcastIds, IndexPasses);

        // Act / Assert
        for (var passIndex = 0; passIndex < batches.Length; passIndex++)
        {
            var pass = passIndex + 1;
            var expectedHours = pass <= 2
                ? new[] { 0, 6, 12, 18 }
                : new[] { 3, 9, 15, 21 };

            foreach (var _ in batches[passIndex])
            {
                YouTubeEnabledHoursForPass(pass).Should().Equal(expectedHours);
            }
        }
    }

    private static int[] YouTubeEnabledHoursForPass(int pass)
    {
        return Enumerable.Range(0, 24)
            .Where(hour =>
            {
                var strategy = new IndexingStrategy(new FixedHourDateTimeService(hour));
                if (!strategy.ResolveYouTube())
                {
                    return false;
                }

                var (firstPass, lastPass) = HourlyIndexingPassSelector.SelectPasses(hour, IndexPasses);
                return pass >= firstPass && pass <= lastPass;
            })
            .ToArray();
    }

    private sealed class FixedHourDateTimeService(int hour) : IDateTimeService
    {
        public int GetHour() => hour;
    }
}
