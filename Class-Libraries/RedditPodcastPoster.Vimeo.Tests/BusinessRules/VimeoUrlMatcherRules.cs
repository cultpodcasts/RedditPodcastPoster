using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Vimeo.Matching;

namespace RedditPodcastPoster.Vimeo.Tests.BusinessRules;

public class VimeoUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Vimeo video URL with a numeric path is a submit URL, so submit can ingest it like Sounds or Archive.")]
    public void numeric_video_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");

        // Act
        var matches = VimeoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Vimeo host URL without a numeric video id is not a submit URL.")]
    public void non_numeric_vimeo_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = VimeoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
