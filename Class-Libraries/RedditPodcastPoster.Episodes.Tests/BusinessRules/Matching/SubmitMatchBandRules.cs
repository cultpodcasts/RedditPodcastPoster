using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// A submitted episode is matched inside a short band around its expected release.
/// A video from last week and a video from years ago must cost the same window.
/// </summary>
public class SubmitMatchBandRules
{
    [Fact(DisplayName =
        "GetSubmitMatchBand is the same width for a release yesterday and a release years ago " +
        "because the YouTube date is the centre of the window, not a floor walked through to today.")]
    public void Recent_and_years_old_releases_use_the_same_band_width()
    {
        // Arrange
        var recent = DomainTestFixture.UtcDateDaysAgo(1);
        var yearsAgo = DomainTestFixture.UtcDateDaysAgo(800);
        var halfWidth = EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold;

        // Act
        var recentBand = EpisodeReleaseTolerance.GetSubmitMatchBand(recent);
        var yearsAgoBand = EpisodeReleaseTolerance.GetSubmitMatchBand(yearsAgo);

        // Assert
        recentBand.Start.Should().Be(recent.Date.Subtract(halfWidth));
        recentBand.End.Should().Be(recent.Date.Add(halfWidth));
        (recentBand.End - recentBand.Start).Should().Be(TimeSpan.FromDays(28));
        (yearsAgoBand.End - yearsAgoBand.Start).Should().Be(recentBand.End - recentBand.Start);
        yearsAgoBand.End.Should().Be(yearsAgo.Date.Add(halfWidth));
        yearsAgoBand.End.Should().BeBefore(DateTime.UtcNow.Date);
    }

    [Fact(DisplayName =
        "IsInSubmitMatchBand accepts a candidate a few days after the expected release and rejects one hundreds of days later " +
        "because publishing lag inside the band can match and a years-apart date cannot win the first pass.")]
    public void Band_includes_nearby_dates_and_excludes_years_apart_dates()
    {
        // Arrange
        var expected = DomainTestFixture.UtcDateDaysAgo(1);

        // Act
        var nearby = EpisodeReleaseTolerance.IsInSubmitMatchBand(expected.AddDays(2), expected);
        var yearsLater = EpisodeReleaseTolerance.IsInSubmitMatchBand(expected.AddDays(400), expected);
        var justInside = EpisodeReleaseTolerance.IsInSubmitMatchBand(expected.Date.AddDays(-14), expected);
        var justOutside = EpisodeReleaseTolerance.IsInSubmitMatchBand(expected.Date.AddDays(-15), expected);

        // Assert
        nearby.Should().BeTrue();
        yearsLater.Should().BeFalse();
        justInside.Should().BeTrue();
        justOutside.Should().BeFalse();
    }
}
