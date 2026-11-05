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
}
