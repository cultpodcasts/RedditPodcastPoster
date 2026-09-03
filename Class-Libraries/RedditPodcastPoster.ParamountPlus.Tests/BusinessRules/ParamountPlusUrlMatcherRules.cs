using FluentAssertions;
using RedditPodcastPoster.ParamountPlus.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.ParamountPlus.Tests.BusinessRules;

public class ParamountPlusUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Paramount+ show slug URL is a submit URL, so submit can ingest a series catalogue page.")]
    public void show_slug_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.paramountplus.com/shows/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = ParamountPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "A locale-prefixed Paramount+ show URL is a submit URL, because regional storefronts use the same catalogue path.")]
    public void locale_prefixed_show_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.paramountplus.com/gb/shows/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = ParamountPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "The Paramount+ /shows index without a slug is not a submit URL.")]
    public void shows_index_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.paramountplus.com/shows");

        // Act
        var matches = ParamountPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A lookalike host that merely contains the letters paramountplus.com is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.paramountplus.com.example.test/shows/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = ParamountPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
