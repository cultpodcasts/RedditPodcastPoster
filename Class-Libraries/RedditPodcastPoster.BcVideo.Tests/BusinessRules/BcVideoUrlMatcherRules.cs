using FluentAssertions;
using RedditPodcastPoster.BcVideo.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.BcVideo.Tests.BusinessRules;

public class BcVideoUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    private string VideoId()
    {
        var raw = new string(_fixture.CreateYouTubeId().Where(char.IsLetterOrDigit).ToArray());
        return (raw + "aaaaaaaaaaaa")[..12];
    }

    private static string Host => "bitchute.com";

    [Fact(DisplayName =
        "A BcVideo /video/{id} URL is a submit URL, so a pasted watch link can be ingested.")]
    public void video_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");

        // Act
        var matches = BcVideoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A BcVideo /embed/{id} URL is a submit URL, because embed links are the same video as /video/{id}.")]
    public void embed_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/embed/{VideoId()}");

        // Act
        var matches = BcVideoUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
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
