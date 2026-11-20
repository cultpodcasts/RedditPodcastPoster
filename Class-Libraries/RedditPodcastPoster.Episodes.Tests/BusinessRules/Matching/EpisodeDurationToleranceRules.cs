using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Duration-band arithmetic for YouTube↔audio pairs (#1470).
/// </summary>
public class EpisodeDurationToleranceRules
{
    [Fact(DisplayName =
        "A six-minute-thirteen-second YouTube cut on a one-hour-eight-minute audio file is inside " +
        "the title-supported 12% band and outside the five-minute title-free cap, because the wider " +
        "band is the greater of five minutes and 12% of the longer length.")]
    public void Hour_long_ad_stripped_youtube_is_inside_title_supported_band_only()
    {
        // Arrange
        var audio = new TimeSpan(1, 8, 44);
        var youTube = new TimeSpan(1, 2, 31);
        var delta = audio - youTube;

        // Act
        var titleSupported = EpisodeDurationTolerance.TitleSupportedYouTubeAudio(audio, youTube);
        var titleFree = EpisodeDurationTolerance.ForYouTubeAudioPair(audio, youTube, titleSupported: false);

        // Assert
        delta.Should().Be(TimeSpan.FromMinutes(6) + TimeSpan.FromSeconds(13));
        titleFree.Should().Be(EpisodeDurationTolerance.CrossPlatform);
        delta.Should().BeGreaterThan(titleFree);
        titleSupported.Should().Be(TimeSpan.FromTicks(
            (long)(audio.Ticks * EpisodeDurationTolerance.TitleSupportedCrossPlatformFraction)));
        delta.Should().BeLessThan(titleSupported);
    }

    [Fact(DisplayName =
        "A 12% cut on a thirty-minute file is three minutes and thirty-six seconds, so the " +
        "title-supported band stays at the five-minute floor, because short episodes must not " +
        "get a looser band than title-free pairs.")]
    public void Short_episode_title_supported_band_is_the_five_minute_floor()
    {
        // Arrange
        var audio = TimeSpan.FromMinutes(30);
        var youTube = TimeSpan.FromMinutes(26) + TimeSpan.FromSeconds(24);

        // Act
        var band = EpisodeDurationTolerance.TitleSupportedYouTubeAudio(audio, youTube);

        // Assert
        TimeSpan.FromTicks((long)(audio.Ticks * EpisodeDurationTolerance.TitleSupportedCrossPlatformFraction))
            .Should().Be(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(36));
        band.Should().Be(EpisodeDurationTolerance.CrossPlatform);
    }
}
