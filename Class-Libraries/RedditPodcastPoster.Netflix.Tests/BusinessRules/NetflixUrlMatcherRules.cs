using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Netflix.Matching;

namespace RedditPodcastPoster.Netflix.Tests.BusinessRules;

public class NetflixUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Netflix title URL is a submit URL, so submit can ingest a title page as a non-podcast episode.")]
    public void title_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");

        // Act
        var matches = NetflixUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Netflix watch URL is a submit URL, the same as a title page.")]
    public void watch_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/watch/{_fixture.CreateAppleId()}");

        // Act
        var matches = NetflixUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Netflix host URL that is not a title or watch path is not a submit URL.")]
    public void browse_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/browse/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = NetflixUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters netflix.com is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com.example.test/title/{_fixture.CreateAppleId()}");

        // Act
        var matches = NetflixUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Theory(DisplayName =
        "Netflix title URLs with or without a country or locale segment compact to the same title id and one canonical URL, " +
        "so us, gb and country-less pastes of one title cannot be stored as separate items (#1517).")]
    [InlineData("https://www.netflix.com/us/title/81774139")]
    [InlineData("https://www.netflix.com/gb/title/81774139")]
    [InlineData("https://www.netflix.com/gb-en/title/81774139")]
    [InlineData("https://www.netflix.com/title/81774139")]
    [InlineData("https://netflix.com/US/title/81774139")]
    public void country_title_urls_share_one_canonical_url(string pasted)
    {
        // Arrange
        var url = new Uri(pasted);

        // Act
        var compact = NetflixUrlMatcher.TryCompactPayload(url);
        var canonical = NetflixUrlMatcher.CanonicalUrl(url);

        // Assert
        compact.Should().Be("81774139");
        canonical.Should().Be(new Uri("https://www.netflix.com/title/81774139"));
    }

    [Fact(DisplayName =
        "A country-prefixed Netflix path that is not a title page does not compact, " +
        "because only /title/ pages have a stable title id.")]
    public void country_browse_path_does_not_compact()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/gb/browse/{_fixture.CreateAppleId()}");

        // Act
        var compact = NetflixUrlMatcher.TryCompactPayload(url);

        // Assert
        compact.Should().BeNull();
    }
}
