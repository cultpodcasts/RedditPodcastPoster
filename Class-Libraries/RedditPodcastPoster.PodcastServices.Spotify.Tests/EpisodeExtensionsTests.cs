using FluentAssertions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests;

public class EpisodeExtensionsTests
{
    [Fact]
    public void GetDescription_IsCorrect()
    {
        // arrange
        var episode = new FullEpisode {HtmlDescription = "text"};
        // act
        var result = episode.GetDescription();
        // assert
        result.Should().Be("text");
    }

    [Fact]
    public void GetDescriptionWithParagraphs_IsCorrect()
    {
        // arrange
        var episode = new FullEpisode {HtmlDescription = "<p>Para 1<p><p>Para 2</p>"};
        // act
        var result = episode.GetDescription();
        // assert
        result.Should().Be("Para 1 Para 2");
    }

    [Fact]
    public void GetDescriptionWithBreaks_IsCorrect()
    {
        // arrange
        var episode = new FullEpisode {HtmlDescription = "Para 1<br />Para 2"};
        // act
        var result = episode.GetDescription();
        // assert
        result.Should().Be("Para 1 Para 2");
    }

    [Fact]
    public void GetDescriptionWithParagraphsAndBreaks_IsCorrect()
    {
        // arrange
        var episode = new FullEpisode {HtmlDescription = "<p>Para 1<br />Para 2<p><p>Para 3</p>"};
        // act
        var result = episode.GetDescription();
        // assert
        result.Should().Be("Para 1 Para 2 Para 3");
    }
}