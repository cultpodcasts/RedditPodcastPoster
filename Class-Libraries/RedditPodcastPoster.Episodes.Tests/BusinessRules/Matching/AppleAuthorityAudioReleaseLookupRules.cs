using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

public class AppleAuthorityAudioReleaseLookupRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "GetAudioReleaseForPlatformLookup does not subtract YouTubePublicationOffset when Apple is " +
        "release authority and the episode already has Apple identity (release is audio-shaped).")]
    public void apple_authority_with_apple_and_youtube_does_not_subtract_delay()
    {
        // Arrange
        var delay = TimeSpan.FromDays(2) + TimeSpan.FromHours(7);
        var podcast = _fixture.CreateAppleReleaseAuthorityPodcast(
            _fixture.CreateAppleId(),
            youTubePublicationOffsetTicks: delay.Ticks);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-4, TimeSpan.FromHours(12));
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release: youTubeRelease);
        var appleId = _fixture.CreateAppleId();
        episode.AppleId = appleId;
        episode.Urls.Apple = _fixture.DefaultAppleUrl(appleId);

        // Act
        var lookup = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);

        // Assert
        lookup.Should().Be(youTubeRelease);
    }

    [Fact(DisplayName =
        "GetAudioReleaseForPlatformLookup still subtracts YouTubePublicationOffset when Apple is " +
        "release authority but the episode has YouTube only (release is still YouTube-shaped).")]
    public void apple_authority_youtube_only_subtracts_delay()
    {
        // Arrange
        var delay = TimeSpan.FromDays(2) + TimeSpan.FromHours(7);
        var podcast = _fixture.CreateAppleReleaseAuthorityPodcast(
            _fixture.CreateAppleId(),
            youTubePublicationOffsetTicks: delay.Ticks);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-4, TimeSpan.FromHours(12));
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release: youTubeRelease);

        // Act
        var lookup = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);

        // Assert
        lookup.Should().Be(youTubeRelease - delay);
    }
}
