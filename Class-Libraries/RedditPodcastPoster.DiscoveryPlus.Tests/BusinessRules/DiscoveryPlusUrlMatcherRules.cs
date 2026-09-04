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
        "A discovery+ movie UUID URL is a submit URL, so film catalogue pages can be dragged into submit.")]
    public void movie_uuid_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/movie/{_fixture.CreateGuid()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A locale-prefixed discovery+ movie URL is a submit URL, because regional film pages use the same /movie/ path.")]
    public void locale_prefixed_movie_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/gb/movie/{_fixture.CreateGuid()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A discovery+ video UUID URL is a submit URL, so episode watch pages can be ingested.")]
    public void video_uuid_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/video/{_fixture.CreateGuid()}");

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
