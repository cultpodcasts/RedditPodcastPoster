using FluentAssertions;
using RedditPodcastPoster.Models.Social;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class CatalogueSocialLineRules
{
    [Fact(DisplayName =
        "A film social post has no parent line, and TV or news names the show or organisation.")]
    public void film_has_no_parent_and_tv_and_news_name_the_parent()
    {
        // Arrange
        const string parent = "Evening Report";

        // Act
        var film = CatalogueSocialLine.ParentLine("Film", parent);
        var tv = CatalogueSocialLine.ParentLine("TvShowEpisode", parent);
        var news = CatalogueSocialLine.ParentLine("NewsReport", "  " + parent);
        var episode = CatalogueSocialLine.ParentLine("Episode", parent);

        // Assert
        film.Should().BeNull();
        tv.Should().Be(parent);
        news.Should().Be(parent);
        episode.Should().Be(parent);
    }
}
