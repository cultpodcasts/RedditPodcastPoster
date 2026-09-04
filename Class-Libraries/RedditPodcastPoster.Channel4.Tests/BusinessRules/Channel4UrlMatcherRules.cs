using FluentAssertions;
using RedditPodcastPoster.Channel4.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Channel4.Tests.BusinessRules;

public class Channel4UrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Channel 4 programme hub URL is a submit URL, so submit can ingest a brand page as a non-podcast episode.")]
    public void programme_hub_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = Channel4UrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Channel 4 on-demand episode URL is a submit URL, the same as a programme hub.")]
    public void on_demand_path_is_submit_url()
    {
        // Arrange
        var url = new Uri(
            $"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}/on-demand/{_fixture.CreateAppleId()}");

        // Act
        var matches = Channel4UrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "An All4 programme URL is a submit URL, because All4 redirects to the same Channel 4 catalogue.")]
    public void all4_programme_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.all4.com/programmes/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = Channel4UrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Channel 4 host URL that is not a programmes path is not a submit URL.")]
    public void categories_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.channel4.com/categories/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = Channel4UrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters channel4.com is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.channel4.com.example.test/programmes/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = Channel4UrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
