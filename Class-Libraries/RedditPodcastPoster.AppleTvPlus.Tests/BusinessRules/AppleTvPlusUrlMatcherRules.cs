using FluentAssertions;
using RedditPodcastPoster.AppleTvPlus.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.AppleTvPlus.Tests.BusinessRules;

public class AppleTvPlusUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An Apple TV+ /{storefront}/show|movie|episode/{slug}/{umc.cmc.*} URL is a submit URL " +
        "for every two-letter storefront, because those paths are catalogue items.")]
    public void show_movie_and_episode_urls_are_submit_urls()
    {
        // Arrange
        var storefronts = new[] { "us", "gb", "de", "fr" };
        var slug = _fixture.CreateYouTubeId();
        var id = $"umc.cmc.{_fixture.CreateYouTubeId().ToLowerInvariant()}";

        // Act / Assert
        foreach (var storefront in storefronts)
        {
            var show = new Uri($"https://tv.apple.com/{storefront}/show/{slug}/{id}");
            var movie = new Uri($"https://tv.apple.com/{storefront}/movie/{slug}/{id}");
            var episode = new Uri($"https://tv.apple.com/{storefront}/episode/{slug}/{id}");

            AppleTvPlusUrlMatcher.IsSubmitUrl(show).Should().BeTrue();
            AppleTvPlusUrlMatcher.IsShowUrl(show).Should().BeTrue();
            AppleTvPlusUrlMatcher.IsSubmitUrl(movie).Should().BeTrue();
            AppleTvPlusUrlMatcher.IsMovieUrl(movie).Should().BeTrue();
            AppleTvPlusUrlMatcher.IsSubmitUrl(episode).Should().BeTrue();
            AppleTvPlusUrlMatcher.IsEpisodeUrl(episode).Should().BeTrue();
        }
    }

    [Fact(DisplayName =
        "An Apple TV+ channel, shelf, or clip URL is not a submit URL, " +
        "because those paths are browse or promo surfaces rather than catalogue titles.")]
    public void channel_shelf_and_clip_are_not_submit_urls()
    {
        // Arrange
        var channel = new Uri("https://tv.apple.com/us/channel/apple-tv/tvs.sbd.4000");
        var shelf = new Uri($"https://tv.apple.com/us/shelf/{_fixture.CreateYouTubeId()}/uts.col.ChartsShows");
        var clip = new Uri(
            $"https://tv.apple.com/us/clip/{_fixture.CreateYouTubeId()}/umc.cmc.{_fixture.CreateYouTubeId().ToLowerInvariant()}");

        // Act / Assert
        AppleTvPlusUrlMatcher.IsSubmitUrl(channel).Should().BeFalse();
        AppleTvPlusUrlMatcher.IsSubmitUrl(shelf).Should().BeFalse();
        AppleTvPlusUrlMatcher.IsSubmitUrl(clip).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters tv.apple.com is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri(
            $"https://tv.apple.com.example.test/us/show/{_fixture.CreateYouTubeId()}/umc.cmc.{_fixture.CreateYouTubeId().ToLowerInvariant()}");

        // Act
        var matches = AppleTvPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
