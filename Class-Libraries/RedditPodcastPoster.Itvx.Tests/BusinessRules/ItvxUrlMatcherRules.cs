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

    [Fact(DisplayName =
        "IsWatchCataloguePath is path-only and still true for a non-itv host with the ITVX watch shape, " +
        "so IsSubmitUrl remains the host∧path conjunction.")]
    public void watch_catalogue_path_is_host_agnostic()
    {
        // Arrange
        var url = new Uri($"https://example.test/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var pathOnly = ItvxUrlMatcher.IsWatchCataloguePath(url);
        var submit = ItvxUrlMatcher.IsSubmitUrl(url);

        // Assert
        pathOnly.Should().BeTrue();
        submit.Should().BeFalse();
    }

    [Fact(DisplayName =
        "IsWatchBrandHubPath is true only for /watch/{brand}/{programmeId} (depth 3), " +
        "not for episode watch paths with a fourth segment.")]
    public void brand_hub_excludes_episode_segment()
    {
        // Arrange
        var brand = _fixture.CreateYouTubeId();
        var programme = _fixture.CreateYouTubeId();
        var hub = new Uri($"https://www.itv.com/watch/{brand}/{programme}");
        var episode = new Uri($"https://www.itv.com/watch/{brand}/{programme}/{_fixture.CreateYouTubeId()}");

        // Act
        var hubIsBrand = ItvxUrlMatcher.IsWatchBrandHubPath(hub);
        var episodeIsBrand = ItvxUrlMatcher.IsWatchBrandHubPath(episode);

        // Assert
        hubIsBrand.Should().BeTrue();
        episodeIsBrand.Should().BeFalse();
    }
}
