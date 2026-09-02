using FluentAssertions;
using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.AmazonPrime.Tests.BusinessRules;

public class AmazonPrimeUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Prime Video detail URL is a submit URL, so submit can ingest a Prime page as a non-podcast episode.")]
    public void primevideo_detail_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = AmazonPrimeUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "An amazon.com Prime Video path is a submit URL.")]
    public void amazon_prime_video_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.amazon.com/gp/video/detail/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = AmazonPrimeUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Prime Video host URL that is not a detail or gp/video path is not a submit URL.")]
    public void primevideo_storefront_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.primevideo.com/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = AmazonPrimeUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters amazon.com is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://notamazon.com/gp/video/detail/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = AmazonPrimeUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
