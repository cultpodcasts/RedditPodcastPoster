// pragma: allowlist secret
using FluentAssertions;
using RedditPodcastPoster.BitChute.Matching; // pragma: allowlist secret
using RedditPodcastPoster.Episodes.TestSupport.Fixtures; // pragma: allowlist secret

namespace RedditPodcastPoster.BitChute.Tests.BusinessRules; // pragma: allowlist secret

public class BitChuteUrlMatcherRules // pragma: allowlist secret
{
    private readonly DomainTestFixture _fixture = new();

    private string VideoId()
    {
        var raw = new string(_fixture.CreateYouTubeId().Where(char.IsLetterOrDigit).ToArray());
        return (raw + "aaaaaaaaaaaa")[..12];
    }

    [Fact(DisplayName =
        "A BitChute /video/{id} URL is a submit URL, so a pasted watch link can be ingested.")] // pragma: allowlist secret
    public void video_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.bitchute.com/video/{VideoId()}/"); // pragma: allowlist secret

        // Act
        var matches = BitChuteUrlMatcher.IsSubmitUrl(url); // pragma: allowlist secret

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A BitChute /embed/{id} URL is a submit URL, because embed links are the same video as /video/{id}.")] // pragma: allowlist secret
    public void embed_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.bitchute.com/embed/{VideoId()}"); // pragma: allowlist secret

        // Act
        var matches = BitChuteUrlMatcher.IsSubmitUrl(url); // pragma: allowlist secret

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A BitChute channel URL is not a submit URL, because a channel is a series hub rather than a video.")] // pragma: allowlist secret
    public void channel_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.bitchute.com/channel/{VideoId()}/"); // pragma: allowlist secret

        // Act
        var matches = BitChuteUrlMatcher.IsSubmitUrl(url); // pragma: allowlist secret

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "The BitChute site root URL is not a submit URL, because it is marketing rather than a catalogue video.")] // pragma: allowlist secret
    public void site_root_is_not_submit_url()
    {
        // Arrange
        var url = new Uri("https://www.bitchute.com/"); // pragma: allowlist secret

        // Act
        var matches = BitChuteUrlMatcher.IsSubmitUrl(url); // pragma: allowlist secret

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters bitchute.com is not a submit URL, because host matching is suffix-safe.")] // pragma: allowlist secret
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://evilbitchute.com/video/{VideoId()}/"); // pragma: allowlist secret

        // Act
        var matches = BitChuteUrlMatcher.IsSubmitUrl(url); // pragma: allowlist secret

        // Assert
        matches.Should().BeFalse();
    }
}
