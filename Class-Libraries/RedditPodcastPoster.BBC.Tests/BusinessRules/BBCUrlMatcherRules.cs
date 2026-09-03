using FluentAssertions;
using RedditPodcastPoster.BBC.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.BBC.Tests.BusinessRules;

public class BbcUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A BBC Sounds play URL is a submit URL, so submit can ingest it without a Spotify/Apple/YouTube identity.")]
    public void sounds_play_url_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = BBCUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
        BBCUrlMatcher.IsSoundsPlayUrl(url).Should().BeTrue();
        BBCUrlMatcher.IsBBCUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A BBC iPlayer episode URL is a submit URL, so submit can ingest a watch page the same way as Sounds.")]
    public void iplayer_episode_url_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = BBCUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
        BBCUrlMatcher.IsIplayerEpisodeUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A BBC host URL that is not Sounds play or iPlayer episode is not a submit URL, " +
        "so news and other BBC pages are not ingested.")]
    public void bbc_news_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = BBCUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
        BBCUrlMatcher.IsBBCUrl(url).Should().BeTrue();
    }
}
