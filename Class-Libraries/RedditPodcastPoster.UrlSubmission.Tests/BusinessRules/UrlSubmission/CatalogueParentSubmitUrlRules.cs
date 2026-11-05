using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlSubmission.Services;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class CatalogueParentSubmitUrlRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Curator catalogue filing as Film, TvShowEpisode, or NewsReport accepts a Channel 4 programme URL, " +
        "because streaming catalogue services can own those playables — not only Spotify, YouTube, or Apple.")]
    public void channel4_programme_url_can_file_under_catalogue_parent()
    {
        // Arrange
        var slug = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.channel4.com/programmes/{slug}");

        // Act
        var allowed = EpisodeUrlRouter.CanFileUnderCatalogueParent(url);

        // Assert
        allowed.Should().BeTrue();
        EpisodeUrlRouter.IsPodcastPlatformUrl(url).Should().BeFalse(
            "Channel 4 is streaming, not a podcast platform; owner auto-routing stays podcast-platform-only.");
    }

    [Fact(DisplayName =
        "Curator catalogue filing as Film, TvShowEpisode, or NewsReport still accepts Spotify episode URLs.")]
    public void spotify_episode_url_can_file_under_catalogue_parent()
    {
        // Arrange
        var url = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl!;

        // Act
        var allowed = EpisodeUrlRouter.CanFileUnderCatalogueParent(url);

        // Assert
        allowed.Should().BeTrue();
    }

    [Fact(DisplayName =
        "An unrecognised host cannot be filed as a Film, TvShowEpisode, or NewsReport via catalogue submit.")]
    public void unknown_host_cannot_file_under_catalogue_parent()
    {
        // Arrange
        var host = _fixture.CreateYouTubeId();
        var url = new Uri($"https://{host}.example.test/{_fixture.CreateYouTubeId()}");

        // Act
        var allowed = EpisodeUrlRouter.CanFileUnderCatalogueParent(url);

        // Assert
        allowed.Should().BeFalse();
    }
}
