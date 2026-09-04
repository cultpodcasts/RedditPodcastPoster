using FluentAssertions;
using RedditPodcastPoster.Fawesome.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Fawesome.Tests.BusinessRules;

public class FawesomeUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Fawesome movie URL with a numeric id is a submit URL, so submit can ingest a free film page.")]
    public void movie_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://fawesome.tv/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = FawesomeUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "A Fawesome tv-shows URL with a numeric id is a submit URL, the same as a movie page.")]
    public void tv_show_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://fawesome.tv/tv-shows/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = FawesomeUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "The Fawesome homepage is not a submit URL, because it is marketing rather than a catalogue title.")]
    public void homepage_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://fawesome.tv/");

        // Act
        var matches = FawesomeUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A lookalike host that merely contains the letters fawesome.tv is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://fawesome.tv.example.test/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = FawesomeUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
