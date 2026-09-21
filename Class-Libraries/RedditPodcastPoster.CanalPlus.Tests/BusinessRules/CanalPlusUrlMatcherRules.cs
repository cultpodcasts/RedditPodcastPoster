using FluentAssertions;
using RedditPodcastPoster.CanalPlus.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.CanalPlus.Tests.BusinessRules;

public class CanalPlusUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Canal+ /{kind}/{slug}/h/{id} URL is a submit URL with or without a two-letter locale prefix, " +
        "because /h/{id} identifies the catalogue item.")]
    public void catalogue_urls_with_content_id_are_submit_urls()
    {
        // Arrange
        var slug = _fixture.CreateYouTubeId();
        var id = $"{_fixture.CreateAppleId()}_{_fixture.CreateAppleId().ToString()[..5]}";
        var plain = new Uri($"https://www.canalplus.com/series/{slug}/h/{id}");
        var localized = new Uri($"https://www.canalplus.com/fr/cinema/{slug}/h/{id}");

        // Act / Assert
        CanalPlusUrlMatcher.IsSubmitUrl(plain).Should().BeTrue();
        CanalPlusUrlMatcher.IsSubmitUrl(localized).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Canal+ browse shelf such as /series or /fr/cinema without /h/{id} is not a submit URL, " +
        "because it does not identify a catalogue item.")]
    public void browse_shelves_are_not_submit_urls()
    {
        // Arrange
        var seriesRoot = new Uri("https://www.canalplus.com/series");
        var cinemaRoot = new Uri("https://www.canalplus.com/fr/cinema/");
        var slugOnly = new Uri($"https://www.canalplus.com/series/{_fixture.CreateYouTubeId()}");

        // Act / Assert
        CanalPlusUrlMatcher.IsSubmitUrl(seriesRoot).Should().BeFalse();
        CanalPlusUrlMatcher.IsSubmitUrl(cinemaRoot).Should().BeFalse();
        CanalPlusUrlMatcher.IsSubmitUrl(slugOnly).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters canalplus.com is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var id = $"{_fixture.CreateAppleId()}_{_fixture.CreateAppleId().ToString()[..5]}";
        var url = new Uri(
            $"https://www.canalplus.com.example.test/series/{_fixture.CreateYouTubeId()}/h/{id}");

        // Act
        var matches = CanalPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
