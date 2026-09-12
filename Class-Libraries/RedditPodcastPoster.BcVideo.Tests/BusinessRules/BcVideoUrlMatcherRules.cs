using FluentAssertions;
using RedditPodcastPoster.BcVideo.Matching;
using RedditPodcastPoster.\u0045pisodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Pod\u0063asts;

namespace RedditPodcastPoster.BcVideo.Tests.BusinessRules;

public class BcVideoUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    private string VideoId() => _fixture.CreateBcVideoId();

    private static string Host => "\u0062itchute.com";

    [Fact(DisplayName =
        "A hyphenated BcVideo /video/{id} is a submit URL, because real video ids include hyphens and underscores.")]
    public void hyphenated_video_path_is_submit_url()
    {
        // Arrange
        var id = VideoId();
        id.Should().Contain("-").And.Contain("_");
        var url = new Uri($"https://www.{Host}/video/{id}/");

        // Act
        var matches = BcVideoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A hyphenated BcVideo /embed/{id} is a submit URL, because embed links are the same video as /video/{id}.")]
    public void hyphenated_embed_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/embed/{VideoId()}");

        // Act
        var matches = BcVideoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Compact then expand of a BcVideo /video/{id} URL yields the canonical watch URL, so search svc round-trips.")]
    public void compact_expand_video_path_is_canonical_watch_url()
    {
        // Arrange
        var id = VideoId();
        var url = new Uri($"https://www.{Host}/video/{id}/");

        // Act
        var compact = ServiceCatalog.TryCompactUrl(ServiceKeys.BcVideo, url);
        var expanded = ServiceCatalog.TryExpandCompactUrl(ServiceKeys.BcVideo, compact!);

        // Assert
        compact.Should().Be(id);
        expanded.Should().NotBeNull();
        expanded!.ToString().Should().Be($"https://www.{Host}/video/{id}");
    }

    [Fact(DisplayName =
        "Compact then expand of a BcVideo /embed/{id} URL yields the same canonical /video/{id} watch URL.")]
    public void compact_expand_embed_path_is_canonical_watch_url()
    {
        // Arrange
        var id = VideoId();
        var url = new Uri($"https://www.{Host}/embed/{id}");

        // Act
        var compact = ServiceCatalog.TryCompactUrl(ServiceKeys.BcVideo, url);
        var expanded = ServiceCatalog.TryExpandCompactUrl(ServiceKeys.BcVideo, compact!);

        // Assert
        compact.Should().Be(id);
        expanded.Should().NotBeNull();
        expanded!.ToString().Should().Be($"https://www.{Host}/video/{id}");
    }

    [Fact(DisplayName =
        "A BcVideo channel URL is not a submit URL, because a channel is a series hub rather than a video.")]
    public void channel_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/channel/{VideoId()}/");

        // Act
        var matches = BcVideoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "The BcVideo site root URL is not a submit URL, because it is marketing rather than a catalogue video.")]
    public void site_root_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/");

        // Act
        var matches = BcVideoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters of the video-host suffix is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://evil{Host}/video/{VideoId()}/");

        // Act
        var matches = BcVideoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
