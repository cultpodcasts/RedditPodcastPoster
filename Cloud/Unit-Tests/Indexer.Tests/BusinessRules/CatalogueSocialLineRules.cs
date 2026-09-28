using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Social;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class CatalogueSocialLineRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A film social post has no parent line. An episode, TV episode, or news report " +
        "names the trimmed show or organisation, and a blank parent is omitted.")]
    public void film_has_no_parent_and_other_kinds_name_the_trimmed_parent()
    {
        // Arrange
        var parent = _fixture.CreateTitle();
        var padded = "  " + parent + "  ";

        // Act
        var film = CatalogueSocialLine.ParentLine(ContentKind.Film, parent);
        var episode = CatalogueSocialLine.ParentLine(ContentKind.Episode, padded);
        var tv = CatalogueSocialLine.ParentLine(ContentKind.TvShowEpisode, padded);
        var news = CatalogueSocialLine.ParentLine(ContentKind.NewsReport, parent);
        var blank = CatalogueSocialLine.ParentLine(ContentKind.NewsReport, "   ");

        // Assert
        film.Should().BeNull();
        episode.Should().Be(parent);
        tv.Should().Be(parent);
        news.Should().Be(parent);
        blank.Should().BeNull();
    }
}
