using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class EpisodeReleaseMatchToleranceTests
{
    [Fact]
    public void GetToleranceTicks_WhenYouTubeIsReleaseAuthorityWithPositiveDelay_UsesTwiceDelayNotFourteenDays()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks
        };

        var tolerance = TimeSpan.FromTicks(
            EpisodeReleaseMatchTolerance.GetToleranceTicks(podcast, TimeSpan.FromHours(1)));

        tolerance.Should().BeLessThan(TimeSpan.FromDays(14));
        tolerance.Should().BeGreaterThan(TimeSpan.FromDays(1));
    }

    [Fact]
    public void GetToleranceTicks_WhenNoYouTubeDelay_UsesFourteenDayThreshold()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube
        };

        var tolerance = TimeSpan.FromTicks(
            EpisodeReleaseMatchTolerance.GetToleranceTicks(podcast, TimeSpan.Zero));

        tolerance.Should().Be(TimeSpan.FromDays(14));
    }
}
