using FluentAssertions;
using RedditPodcastPoster.Hulu.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Hulu.Tests.BusinessRules;

public class HuluUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Hulu /series/{slug} or /movie/{slug} URL is a submit URL, " +
        "because those paths are catalogue hubs rather than marketing shelves.")]
    public void series_and_movie_urls_are_submit_urls()
    {
        // Arrange
        var series = new Uri($"https://www.hulu.com/series/{_fixture.CreateYouTubeId()}-{_fixture.CreateGuid()}");
        var movie = new Uri($"https://www.hulu.com/movie/{_fixture.CreateYouTubeId()}-{_fixture.CreateGuid()}");

        // Act / Assert
        HuluUrlMatcher.IsSubmitUrl(series).Should().BeTrue();
        HuluUrlMatcher.IsSeriesUrl(series).Should().BeTrue();
        HuluUrlMatcher.IsSubmitUrl(movie).Should().BeTrue();
        HuluUrlMatcher.IsMovieUrl(movie).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Hulu /watch/{id} URL is a submit URL, while /watch/offers is rejected, " +
        "because offers is a commerce shell rather than a catalogue item.")]
    public void watch_id_is_submit_url_but_offers_is_not()
    {
        // Arrange
        var watch = new Uri($"https://www.hulu.com/watch/{_fixture.CreateGuid()}");
        var offers = new Uri("https://www.hulu.com/watch/offers");

        // Act / Assert
        HuluUrlMatcher.IsSubmitUrl(watch).Should().BeTrue();
        HuluUrlMatcher.IsWatchUrl(watch).Should().BeTrue();
        HuluUrlMatcher.IsSubmitUrl(offers).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Hulu category shelf such as /series or /hub/{name} is not a submit URL, " +
        "because it has no catalogue slug.")]
    public void category_shelf_is_not_submit_url()
    {
        // Arrange
        var seriesRoot = new Uri("https://www.hulu.com/series");
        var hub = new Uri($"https://www.hulu.com/hub/{_fixture.CreateYouTubeId()}");

        // Act / Assert
        HuluUrlMatcher.IsSubmitUrl(seriesRoot).Should().BeFalse();
        HuluUrlMatcher.IsSubmitUrl(hub).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters hulu.com is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.hulu.com.example.test/series/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = HuluUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
