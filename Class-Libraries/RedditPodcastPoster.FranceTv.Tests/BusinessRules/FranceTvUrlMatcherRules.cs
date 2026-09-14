using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.FranceTv.Matching;

namespace RedditPodcastPoster.FranceTv.Tests.BusinessRules;

public class FranceTvUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A France TV series hub URL /{channel}/{show-slug}/ is a submit URL, so brand pages can be ingested.")]
    public void series_hub_is_submit_url()
    {
        // Arrange
        var url = new Uri(
            $"https://www.france.tv/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/");

        // Act
        var matches = FranceTvUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
        FranceTvUrlMatcher.IsSeriesUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A France TV episode URL /{channel}/{show}/{digits}-{slug}.html is a submit URL, the same as a series hub.")]
    public void episode_html_is_submit_url()
    {
        // Arrange
        var url = new Uri(
            $"https://www.france.tv/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/" +
            $"{_fixture.CreateAppleId()}-{_fixture.CreateYouTubeId()}.html");

        // Act
        var matches = FranceTvUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
        FranceTvUrlMatcher.IsEpisodeUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A France TV host URL with only one path segment is not a submit URL, because it is not a catalogue hub or episode.")]
    public void single_segment_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.france.tv/{_fixture.CreateYouTubeId()}/");

        // Act
        var matches = FranceTvUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters france.tv is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri(
            $"https://www.france.tv.example.test/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/");

        // Act
        var matches = FranceTvUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
