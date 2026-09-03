using FluentAssertions;
using RedditPodcastPoster.HboMax.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.HboMax.Tests.BusinessRules;

public class HboMaxUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Max show URL is a submit URL, so submit can ingest an HBO Max series page.")]
    public void max_show_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.max.com/shows/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = HboMaxUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "An hbomax.com series URL is a submit URL, because the legacy host still serves catalogue pages.")]
    public void hbomax_series_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.hbomax.com/series/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = HboMaxUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "The Max homepage is not a submit URL, because it is marketing rather than a catalogue title.")]
    public void max_home_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.max.com/");

        // Act
        var matches = HboMaxUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A lookalike host that merely contains the letters max.com is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.max.com.example.test/shows/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = HboMaxUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
