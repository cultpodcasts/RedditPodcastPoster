using FluentAssertions;
using RedditPodcastPoster.PlaySuisse.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.PlaySuisse.Tests.BusinessRules;

public class PlaySuisseUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Play Suisse watch URL with a numeric id is a submit URL, so submit can ingest a catalogue title.")]
    public void watch_id_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.playsuisse.ch/watch/{_fixture.CreateAppleId()}");

        // Act
        var matches = PlaySuisseUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "A Play Suisse detail URL with a numeric id is a submit URL, because films use /detail rather than /watch.")]
    public void detail_id_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.playsuisse.ch/detail/{_fixture.CreateAppleId()}");

        // Act
        var matches = PlaySuisseUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "A locale-prefixed Play Suisse watch URL is a submit URL, because regional paths still identify a title id.")]
    public void locale_watch_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.playsuisse.ch/en/watch/{_fixture.CreateAppleId()}");

        // Act
        var matches = PlaySuisseUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "The Play Suisse homepage is not a submit URL, because it is marketing rather than a catalogue title.")]
    public void homepage_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.playsuisse.ch/");

        // Act
        var matches = PlaySuisseUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
