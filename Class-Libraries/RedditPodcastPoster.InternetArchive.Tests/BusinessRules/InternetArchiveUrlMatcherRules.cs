using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.InternetArchive.Matching;

namespace RedditPodcastPoster.InternetArchive.Tests.BusinessRules;

public class InternetArchiveUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An Internet Archive details URL is a submit URL, so submit can ingest an archive item.")]
    public void details_url_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://archive.org/details/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = InternetArchiveUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
        InternetArchiveUrlMatcher.IsDetailsUrl(url).Should().BeTrue();
        InternetArchiveUrlMatcher.IsInternetArchiveUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "An archive.org URL that is not a details item is not a submit URL.")]
    public void search_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://archive.org/search?query={_fixture.CreateYouTubeId()}");

        // Act
        var matches = InternetArchiveUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
        InternetArchiveUrlMatcher.IsInternetArchiveUrl(url).Should().BeTrue();
    }
}
