using FluentAssertions;
using RedditPodcastPoster.DisneyPlus.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.DisneyPlus.Tests.BusinessRules;

public class DisneyPlusUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Disney+ browse entity URL is a submit URL, so submit can ingest a catalogue entity page.")]
    public void browse_entity_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.disneyplus.com/browse/entity-{_fixture.CreateGuid()}");

        // Act
        var matches = DisneyPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "A Disney+ series slug URL is a submit URL, the same as a browse entity page.")]
    public void series_slug_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.disneyplus.com/series/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = DisneyPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "The Disney+ homepage is not a submit URL, because it is marketing rather than a catalogue title.")]
    public void home_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.disneyplus.com/");

        // Act
        var matches = DisneyPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A lookalike host that merely contains the letters disneyplus.com is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.disneyplus.com.example.test/series/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = DisneyPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
