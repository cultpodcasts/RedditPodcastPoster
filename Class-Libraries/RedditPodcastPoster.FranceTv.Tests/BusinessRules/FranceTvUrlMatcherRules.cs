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

    [Fact(DisplayName =
        "A three-segment France TV path that is not a digits-slug .html file is not a submit URL, " +
        "because episodes require {digits}-{slug}.html and series hubs are exactly two segments.")]
    public void three_segment_non_html_is_not_submit_url()
    {
        // Arrange
        var url = new Uri(
            $"https://www.france.tv/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/" +
            $"{_fixture.CreateYouTubeId()}/");

        // Act
        var matches = FranceTvUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
        FranceTvUrlMatcher.IsEpisodeUrl(url).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A France TV .html leaf without a leading digit id, or ending in .htm, is not an episode submit URL.")]
    public void episode_leaf_requires_digit_prefix_and_html_extension()
    {
        // Arrange
        var channel = _fixture.CreateYouTubeId();
        var show = _fixture.CreateYouTubeId();
        var slug = _fixture.CreateYouTubeId();
        // Letter-prefixed leaf so the segment before '-' is never all digits.
        var missingDigits = new Uri($"https://www.france.tv/{channel}/{show}/ep-{slug}.html");
        var htmOnly = new Uri(
            $"https://www.france.tv/{channel}/{show}/{_fixture.CreateAppleId()}-{slug}.htm");

        // Act / Assert
        FranceTvUrlMatcher.IsSubmitUrl(missingDigits).Should().BeFalse();
        FranceTvUrlMatcher.IsEpisodeUrl(missingDigits).Should().BeFalse();
        FranceTvUrlMatcher.IsSubmitUrl(htmOnly).Should().BeFalse();
        FranceTvUrlMatcher.IsEpisodeUrl(htmOnly).Should().BeFalse();
    }
}
