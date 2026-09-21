using FluentAssertions;
using RedditPodcastPoster.Peacock.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Peacock.Tests.BusinessRules;

public class PeacockUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Peacock /watch/asset/{kind}/{slug}/{numericId} URL is a submit URL, " +
        "because asset pages identify a catalogue title.")]
    public void asset_url_is_submit_url()
    {
        // Arrange
        var slug = _fixture.CreateYouTubeId();
        var id = $"{_fixture.CreateAppleId()}{_fixture.CreateAppleId()}";
        var url = new Uri($"https://www.peacocktv.com/watch/asset/tv/{slug}/{id}");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsAssetUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsPlaybackUrl(url).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Peacock /watch/playback/vod/{id} URL is a submit URL, " +
        "because playback pages resolve to a catalogue asset.")]
    public void playback_url_is_submit_url()
    {
        // Arrange
        var id = $"GMO_{_fixture.CreateAppleId()}";
        var url = new Uri($"https://www.peacocktv.com/watch/playback/vod/{id}");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsPlaybackUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Peacock app shell such as /watch/home is not a submit URL, " +
        "because it is not a catalogue asset.")]
    public void watch_home_is_not_submit_url()
    {
        // Arrange
        var url = new Uri("https://www.peacocktv.com/watch/home");

        // Act
        var matches = PeacockUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters peacocktv.com is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri(
            $"https://www.peacocktv.com.example.test/watch/asset/tv/{_fixture.CreateYouTubeId()}/{_fixture.CreateAppleId()}{_fixture.CreateAppleId()}");

        // Act
        var matches = PeacockUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
