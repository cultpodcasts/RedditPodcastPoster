using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Services;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>The stored form the catalogue ownership checks compare a pasted URL with. URLs are synthetic.</summary>
public class PodcastServiceUrlNormaliserRules
{
    [Theory(DisplayName =
        "When an Apple URL has no /podcast path segment (a channel or browse page), the stored form is the URL unchanged instead of an exception, because every Apple submit now reaches the ownership check.")]
    [InlineData("https://podcasts.apple.com/us/channel/synthetic-channel/id1000000000001")]
    [InlineData("https://podcasts.apple.com/us/browse")]
    public void apple_url_without_a_podcast_segment_is_unchanged(string url)
    {
        // Arrange
        var uri = new Uri(url);

        // Act
        var stored = PodcastServiceUrlNormaliser.Stored(uri, ServiceKeys.Apple);

        // Assert
        stored.Should().Be(uri);
    }

    [Fact(DisplayName =
        "When an Apple episode URL carries a storefront segment and the &uo=4 suffix, the stored form drops both, matching how Apple episode links are stored.")]
    public void apple_episode_url_is_cleaned()
    {
        // Arrange
        var uri = new Uri("https://podcasts.apple.com/us/podcast/synthetic-show/id1000000000002?i=1000000000003&uo=4");

        // Act
        var stored = PodcastServiceUrlNormaliser.Stored(uri, ServiceKeys.Apple);

        // Assert
        stored.Should().Be(new Uri("https://podcasts.apple.com/podcast/synthetic-show/id1000000000002?i=1000000000003"));
    }
}
