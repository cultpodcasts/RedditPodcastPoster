using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

public class CatalogueParentSameTitleReleaseToleranceRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a catalogue parent's transient podcast has zero publishing delay, GetToleranceTicks uses the tight " +
        "same-title window and Spotify catalogue day tolerance is same-day only, because news and TV reuse short " +
        "titles across days and must not attach last week's audio (#1161). Ordinary zero-delay podcasts stay at 14 days.")]
    public void catalogue_parent_zero_delay_uses_same_day_title_matching()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        podcast.CatalogueParentSameTitleMatching = true;
        var ordinary = _fixture.CreatePodcast();
        var expectedRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var sameDayMidnight = expectedRelease.Date;
        var oneDayEarlier = expectedRelease.AddDays(-1);
        var catalogueTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());

        // Act
        var ordinaryTicks = EpisodeReleaseTolerance.GetToleranceTicks(ordinary, _fixture.CreateDuration());
        var dayTolerance = EpisodeReleaseTolerance.GetSpotifyCatalogueDayTolerance(podcast);
        var sameDayMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            sameDayMidnight, expectedRelease, catalogueTicks, podcast);
        var oneDayMatches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            oneDayEarlier, expectedRelease, catalogueTicks, podcast);

        // Assert
        catalogueTicks.Should().Be(EpisodeReleaseTolerance.CatalogueParentSameTitleReleaseTolerance.Ticks);
        ordinaryTicks.Should().Be(EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks);
        dayTolerance.Should().Be(0);
        sameDayMatches.Should().BeTrue();
        oneDayMatches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a catalogue parent's expected release is late on UTC day D and Spotify catalogue is date-only midnight " +
        "on D+1 within the 3h same-title tick band, SpotifyCatalogueReleaseMatches returns false, because the day " +
        "gate is authoritative and must not bridge the calendar-day boundary for reused news titles (#1161).")]
    public void catalogue_parent_rejects_spotify_midnight_on_next_day_within_three_hour_band()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        podcast.CatalogueParentSameTitleMatching = true;
        var expectedLateOnD = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(22));
        var spotifyMidnightNextDay = expectedLateOnD.Date.AddDays(1);
        var catalogueTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());
        Math.Abs((spotifyMidnightNextDay - expectedLateOnD).Ticks)
            .Should().BeLessThan(catalogueTicks, "sanity: the absolute gap sits inside the 3h band");

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            spotifyMidnightNextDay, expectedLateOnD, catalogueTicks, podcast);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a catalogue parent's expected release is late on UTC day D and Apple catalogue is early on D+1 within " +
        "the 3h same-title tick band, SpotifyCatalogueReleaseMatches returns false, because Apple catch-up shares " +
        "the same day-authoritative gate and must not bridge midnight for reused news titles (#1161).")]
    public void catalogue_parent_rejects_apple_across_midnight_within_three_hour_band()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        podcast.CatalogueParentSameTitleMatching = true;
        var expectedLateOnD = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(22));
        var appleEarlyNextDay = expectedLateOnD.Date.AddDays(1).AddMinutes(30);
        var catalogueTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, _fixture.CreateDuration());
        Math.Abs((appleEarlyNextDay - expectedLateOnD).Ticks)
            .Should().BeLessThan(catalogueTicks, "sanity: the absolute gap sits inside the 3h band");

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            appleEarlyNextDay, expectedLateOnD, catalogueTicks, podcast);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an ordinary zero-delay podcast has expected release late on UTC day D and Spotify catalogue is " +
        "date-only midnight on D+1 within the tick band, SpotifyCatalogueReleaseMatches still returns true, because " +
        "ordinary podcasts keep the existing tick fallback across the calendar-day boundary.")]
    public void ordinary_podcast_keeps_tick_fallback_across_midnight()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var expectedLateOnD = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(22));
        var spotifyMidnightNextDay = expectedLateOnD.Date.AddDays(1);
        var ticks = EpisodeReleaseTolerance.SameReleaseThreshold.Ticks;
        Math.Abs((spotifyMidnightNextDay - expectedLateOnD).Ticks)
            .Should().BeLessThan(ticks, "sanity: the absolute gap sits inside the 3h band");

        // Act
        var matches = EpisodeReleaseTolerance.SpotifyCatalogueReleaseMatches(
            spotifyMidnightNextDay, expectedLateOnD, ticks, podcast);

        // Assert
        matches.Should().BeTrue();
    }
}
