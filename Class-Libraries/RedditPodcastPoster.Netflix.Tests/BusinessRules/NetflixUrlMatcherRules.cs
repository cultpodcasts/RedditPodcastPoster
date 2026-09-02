using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Netflix.Matching;

namespace RedditPodcastPoster.Netflix.Tests.BusinessRules;

public class NetflixUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Netflix title URL is a submit URL, so submit can ingest a title page as a non-podcast episode.")]
    public void title_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");

        // Act
        var matches = NetflixUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Netflix watch URL is a submit URL, the same as a title page.")]
    public void watch_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/watch/{_fixture.CreateAppleId()}");

        // Act
        var matches = NetflixUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Netflix host URL that is not a title or watch path is not a submit URL.")]
    public void browse_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/browse/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = NetflixUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
