using FluentAssertions;
using RedditPodcastPoster.AbcIview.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.AbcIview.Tests.BusinessRules;

public class AbcIviewUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An ABC iview /video/{id} URL is a submit URL and compact payload, " +
        "so search can encode the catalogue id without storing the full URL.")]
    public void video_path_is_submit_url_and_compacts()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var url = new Uri($"https://iview.abc.net.au/video/{videoId}");

        // Act
        var matches = AbcIviewUrlMatcher.IsSubmitUrl(url);
        var compact = AbcIviewUrlMatcher.TryCompactPayload(url);
        var expanded = AbcIviewUrlMatcher.TryExpandPayload(videoId);

        // Assert
        matches.Should().BeTrue();
        compact.Should().Be(videoId);
        expanded.Should().Be(url);
    }

    [Fact(DisplayName =
        "An ABC iview /show/{slug} hub is a submit URL without a compact video id, " +
        "so series pages can still resolve as streaming membership.")]
    public void show_hub_is_submit_url_without_compact()
    {
        // Arrange
        var slug = _fixture.CreateYouTubeId();
        var url = new Uri($"https://iview.abc.net.au/show/{slug}");

        // Act
        var matches = AbcIviewUrlMatcher.IsSubmitUrl(url);
        var compact = AbcIviewUrlMatcher.TryCompactPayload(url);

        // Assert
        matches.Should().BeTrue();
        compact.Should().BeNull();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters iview.abc.net.au is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://iview.abc.net.au.example.test/video/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = AbcIviewUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "An ABC iview marketing root or shallow path is not a submit URL, " +
        "because only /video/{id} and /show/{slug} are catalogue destinations.")]
    public void shallow_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri("https://iview.abc.net.au/");

        // Act
        var matches = AbcIviewUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
