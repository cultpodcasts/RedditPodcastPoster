using FluentAssertions;
using RedditPodcastPoster.Itvx.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Itvx.Tests.BusinessRules;

public class ItvxUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An ITVX watch URL with a programme slug and brand id is a submit URL, so submit can ingest an ITVX catalogue page.")]
    public void watch_slug_and_id_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = ItvxUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "An ITVX watch URL that includes an episode id is a submit URL, the same as a brand page.")]
    public void watch_episode_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = ItvxUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "An ITV news path is not a submit URL, because news articles are not catalogue titles.")]
    public void news_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.itv.com/watch/news/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = ItvxUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A lookalike host that merely contains the letters itv.com is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.itv.com.example.test/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = ItvxUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
