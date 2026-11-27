using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class WbdCatalogueSiblingUrlRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Discovery+ episode URL with shared catalogue UUIDs rewrites to the Max show path on www.max.com, " +
        "because Discovery+ and HBO Max share WBD catalogue identifiers.")]
    public void discoveryplus_episode_rewrites_to_max()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var slug = _fixture.CreateYouTubeId();
        var url = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-{slug}/{episodeId}?x=1#frag");

        // Act
        var found = WbdCatalogueSiblingUrls.TryGet(
            StreamingService.DiscoveryPlus, url, out var sibling, out var siblingUrl);

        // Assert
        found.Should().BeTrue();
        sibling.Should().Be(StreamingService.HboMax);
        siblingUrl.Should().Be(
            new Uri($"https://www.max.com/show/{showId}/s1/e1-{slug}/{episodeId}"));
    }

    [Fact(DisplayName =
        "A Max /shows/ hub URL with a catalogue UUID rewrites to Discovery+ /show/, " +
        "because Max plural hubs map to the Discovery+ singular catalogue path.")]
    public void max_shows_hub_rewrites_to_discoveryplus_show()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var url = new Uri($"https://www.max.com/shows/{showId}");

        // Act
        var found = WbdCatalogueSiblingUrls.TryGet(
            StreamingService.HboMax, url, out var sibling, out var siblingUrl);

        // Assert
        found.Should().BeTrue();
        sibling.Should().Be(StreamingService.DiscoveryPlus);
        siblingUrl.Should().Be(new Uri($"https://www.discoveryplus.com/show/{showId}"));
    }

    [Fact(DisplayName =
        "A Discovery+ movie UUID URL rewrites to Max /movie/, so film catalogue pages get the sibling link.")]
    public void discoveryplus_movie_rewrites_to_max()
    {
        // Arrange
        var movieId = _fixture.CreateGuid();
        var url = new Uri($"https://www.discoveryplus.com/gb/movie/{movieId}");

        // Act
        var found = WbdCatalogueSiblingUrls.TryGet(
            StreamingService.DiscoveryPlus, url, out var sibling, out var siblingUrl);

        // Assert
        found.Should().BeTrue();
        sibling.Should().Be(StreamingService.HboMax);
        siblingUrl.Should().Be(new Uri($"https://www.max.com/movie/{movieId}"));
    }

    [Fact(DisplayName =
        "A Discovery+ show slug without a catalogue UUID does not produce a Max sibling, " +
        "because only WBD UUID paths are shared across hosts.")]
    public void discoveryplus_slug_without_guid_has_no_sibling()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");

        // Act
        var found = WbdCatalogueSiblingUrls.TryGet(
            StreamingService.DiscoveryPlus, url, out _, out _);

        // Assert
        found.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Discovery+ /video/ UUID path does not produce a Max sibling, " +
        "because Max has no matching /video/ catalogue shape.")]
    public void discoveryplus_video_path_has_no_sibling()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/video/{_fixture.CreateGuid()}");

        // Act
        var found = WbdCatalogueSiblingUrls.TryGet(
            StreamingService.DiscoveryPlus, url, out _, out _);

        // Assert
        found.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Max /series/ path does not produce a Discovery+ sibling, " +
        "because Discovery+ has no /series/ catalogue counterpart.")]
    public void max_series_path_has_no_sibling()
    {
        // Arrange
        var url = new Uri($"https://www.max.com/series/{_fixture.CreateGuid()}");

        // Act
        var found = WbdCatalogueSiblingUrls.TryGet(
            StreamingService.HboMax, url, out _, out _);

        // Assert
        found.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Netflix URL does not produce a WBD sibling, because the pairing is Discovery+ and HBO Max only.")]
    public void netflix_has_no_wbd_sibling()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}");

        // Act
        var found = WbdCatalogueSiblingUrls.TryGet(
            StreamingService.Netflix, url, out _, out _);

        // Assert
        found.Should().BeFalse();
    }
}
