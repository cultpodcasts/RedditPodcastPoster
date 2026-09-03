using FluentAssertions;
using RedditPodcastPoster.DiscoveryPlus.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.DiscoveryPlus.Tests.BusinessRules;

public class DiscoveryPlusUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A discovery+ show slug URL is a submit URL, so submit can ingest a series catalogue page.")]
    public void show_slug_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "A locale-prefixed discovery+ show URL is a submit URL, because regional storefronts use the same catalogue path.")]
    public void locale_prefixed_show_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/gb/show/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "The discovery+ homepage is not a submit URL, because it is marketing rather than a catalogue title.")]
    public void home_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A lookalike host that merely contains the letters discoveryplus.com is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com.example.test/show/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
