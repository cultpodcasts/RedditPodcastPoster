using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// Search criteria must carry the platform they originated from so downstream Apple/Spotify lookups
/// can shift a YouTube publish date back by the podcast's publishing delay (Jul 2026 incident).
/// </summary>
public class SearchCriteriaSourceAuthorityRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Criteria built from a resolved YouTube item carry SourceAuthority YouTube " +
        "because audio lookups must anchor on the delay-shifted audio day.")]
    public void youtube_item_criteria_carry_youtube_source_authority()
    {
        // Arrange
        var item = CreateYouTubeItem();

        // Act
        var criteria = item.ToPodcastServiceSearchCriteria();

        // Assert
        criteria.SourceAuthority.Should().Be(Service.YouTube);
    }

    [Fact(DisplayName =
        "Criteria built from resolved Spotify and Apple items carry their own source authority " +
        "so no YouTube delay shift is applied to audio-sourced submissions.")]
    public void audio_item_criteria_carry_their_source_authority()
    {
        // Act
        var spotifyCriteria = CreateSpotifyItem().ToPodcastServiceSearchCriteria();
        var appleCriteria = CreateAppleItem().ToPodcastServiceSearchCriteria();

        // Assert
        spotifyCriteria.SourceAuthority.Should().Be(Service.Spotify);
        appleCriteria.SourceAuthority.Should().Be(Service.Apple);
    }

    [Fact(DisplayName =
        "Merging cross-service results into YouTube-sourced criteria preserves SourceAuthority YouTube " +
        "because the submission's origin platform does not change when other services resolve.")]
    public void merge_preserves_source_authority()
    {
        // Arrange
        var criteria = CreateYouTubeItem().ToPodcastServiceSearchCriteria();

        // Act
        var afterSpotifyMerge = criteria.Merge(CreateSpotifyItem());
        var afterAppleMerge = afterSpotifyMerge.Merge(CreateAppleItem());

        // Assert
        afterSpotifyMerge.SourceAuthority.Should().Be(Service.YouTube);
        afterAppleMerge.SourceAuthority.Should().Be(Service.YouTube);
    }

    private CategorisedYouTubeItem CreateYouTubeItem()
    {
        var youTubeId = _fixture.CreateYouTubeId();
        return new CategorisedYouTubeItem(
            _fixture.CreateYouTubeChannelId(),
            youTubeId,
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            DomainTestFixture.UtcAtTime(-1, new TimeSpan(13, 48, 0)),
            _fixture.CreateDuration(),
            _fixture.DefaultYouTubeUrl(youTubeId),
            false,
            null,
            null);
    }

    private CategorisedSpotifyItem CreateSpotifyItem()
    {
        var spotifyId = _fixture.CreateSpotifyId();
        return new CategorisedSpotifyItem(
            _fixture.CreateSpotifyId(),
            spotifyId,
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            DomainTestFixture.UtcDateDaysAgo(8),
            _fixture.CreateDuration(),
            _fixture.DefaultSpotifyUrl(spotifyId),
            false,
            null);
    }

    private CategorisedAppleItem CreateAppleItem()
    {
        var appleId = _fixture.CreateAppleId();
        return new CategorisedAppleItem(
            _fixture.CreateAppleId(),
            appleId,
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            _fixture.CreateTitle(),
            DomainTestFixture.UtcAtTime(-8, new TimeSpan(13, 0, 0)),
            _fixture.CreateDuration(),
            _fixture.DefaultAppleUrl(appleId),
            false,
            null);
    }
}
