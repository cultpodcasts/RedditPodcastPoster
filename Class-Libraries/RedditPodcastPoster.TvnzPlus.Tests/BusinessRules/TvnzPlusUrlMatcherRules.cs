using FluentAssertions;
using RedditPodcastPoster.TvnzPlus.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.TvnzPlus.Tests.BusinessRules;

public class TvnzPlusUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A TVNZ+ show slug URL is a submit URL, so submit can ingest a series catalogue page.")]
    public void show_slug_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.tvnz.co.nz/shows/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = TvnzPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "The TVNZ+ /shows index without a slug is not a submit URL.")]
    public void shows_index_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.tvnz.co.nz/shows");

        // Act
        var matches = TvnzPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A TVNZ news path is not a submit URL, because news articles are not catalogue titles.")]
    public void news_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.tvnz.co.nz/news/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = TvnzPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A lookalike host that merely contains the letters tvnz.co.nz is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.tvnz.co.nz.example.test/shows/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = TvnzPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
