using FluentAssertions;
using RedditPodcastPoster.Peacock.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Peacock.Tests.BusinessRules;

public class PeacockUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "CanonicalUrl rewrites /watch/asset episode paths to /watch-online, " +
        "because US SEO pages SSR meta while asset pages soft-wall.")]
    public void canonical_url_rewrites_asset_episode_to_watch_online()
    {
        // Arrange
        var asset = new Uri(
            "https://www.peacocktv.com/watch/asset/tv/the-office-uk/8893980556248533112/seasons/1/episodes/work-experience-episode-2/9694b7a9-ffae-3b84-9606-5f852ccffee0");
        var expected = new Uri(
            "https://www.peacocktv.com/watch-online/tv/the-office-uk/8893980556248533112/seasons/1/episodes/work-experience-episode-2/9694b7a9-ffae-3b84-9606-5f852ccffee0");

        // Act / Assert
        PeacockUrlMatcher.TryToWatchOnlineUrl(asset).Should().Be(expected);
        PeacockUrlMatcher.CanonicalUrl(asset).Should().Be(expected);
        PeacockUrlMatcher.CanonicalUrl(expected).Should().Be(expected);
    }

    [Fact(DisplayName =
        "CanonicalUrl rewrites /watch/asset/movie paths to /watch-online/movies, " +
        "matching contract prepareUrlRewrites.peacock.segmentRemaps movie→movies.")]
    public void canonical_url_rewrites_asset_movie_to_watch_online_movies()
    {
        // Arrange
        var slug = _fixture.CreateYouTubeId();
        var id = "f45c2853-4230-3910-aa53-51ac37f5a788";
        var asset = new Uri($"https://www.peacocktv.com/watch/asset/movie/{slug}/{id}");
        var expected = new Uri($"https://www.peacocktv.com/watch-online/movies/{slug}/{id}");

        // Act / Assert
        PeacockUrlMatcher.TryToWatchOnlineUrl(asset).Should().Be(expected);
        PeacockUrlMatcher.CanonicalUrl(asset).Should().Be(expected);
        PeacockUrlMatcher.CanonicalUrl(expected).Should().Be(expected);
    }

    [Fact(DisplayName =
        "A Peacock /watch/asset/tv/{slug}/{id}/seasons/.../episodes/.../{id} URL is a submit URL, " +
        "because signed-in episode deep links identify the same catalogue item as watch-online.")]
    public void asset_episode_deep_link_is_submit_url()
    {
        // Arrange
        var url = new Uri(
            "https://www.peacocktv.com/watch/asset/tv/the-office-uk/8893980556248533112/seasons/1/episodes/work-experience-episode-2/9694b7a9-ffae-3b84-9606-5f852ccffee0");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsAssetUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Peacock /watch-online/movies/{slug}/{uuid} URL is a submit URL, " +
        "because US SEO movie pages SSR catalogue meta for prepare.")]
    public void watch_online_movie_url_is_submit_url()
    {
        // Arrange
        var slug = _fixture.CreateYouTubeId();
        var url = new Uri(
            $"https://www.peacocktv.com/watch-online/movies/{slug}/f45c2853-4230-3910-aa53-51ac37f5a788");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsWatchOnlineUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsAssetUrl(url).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Peacock /watch-online/tv/{slug}/{id}/seasons/.../episodes/.../{uuid} URL is a submit URL, " +
        "because US SEO episode pages SSR catalogue meta for prepare.")]
    public void watch_online_episode_url_is_submit_url()
    {
        // Arrange
        var showSlug = _fixture.CreateYouTubeId();
        var episodeSlug = _fixture.CreateYouTubeId();
        var showId = $"{_fixture.CreateAppleId()}{_fixture.CreateAppleId()}";
        var url = new Uri(
            $"https://www.peacocktv.com/watch-online/tv/{showSlug}/{showId}/seasons/1/episodes/{episodeSlug}/9694b7a9-ffae-3b84-9606-5f852ccffee0");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsWatchOnlineUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Peacock /watch-online shelf without a catalogue id is not a submit URL, " +
        "because it is not a title or episode page.")]
    public void watch_online_shelf_is_not_submit_url()
    {
        // Arrange
        var url = new Uri("https://www.peacocktv.com/watch-online/tv");

        // Act
        var matches = PeacockUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Peacock /watch/asset/{kind}/{slug}/{numericId} URL is a submit URL, " +
        "because asset pages identify a catalogue title.")]
    public void asset_url_is_submit_url()
    {
        // Arrange
        var slug = _fixture.CreateYouTubeId();
        var id = $"{_fixture.CreateAppleId()}{_fixture.CreateAppleId()}";
        var url = new Uri($"https://www.peacocktv.com/watch/asset/tv/{slug}/{id}");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsAssetUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsPlaybackUrl(url).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Peacock /watch/asset/tv/{slug}/{uuid} URL is a submit URL, " +
        "because some catalogue titles use a UUID asset id.")]
    public void asset_uuid_url_is_submit_url()
    {
        // Arrange
        var slug = _fixture.CreateYouTubeId();
        var url = new Uri(
            $"https://www.peacocktv.com/watch/asset/tv/{slug}/f4ea0790-2f7a-34ee-ba2c-30ff9ed2c03d");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsAssetUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Peacock /watch/playback/vod/{gmo}/{streamId} URL is a submit URL, " +
        "because playback deep links include a stream id after the GMO code.")]
    public void playback_url_with_stream_id_is_submit_url()
    {
        // Arrange
        var url = new Uri(
            "https://www.peacocktv.com/watch/playback/vod/GMO_00000000391471_01/8e388082-094f-3974-951b-03332f1a1e67");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsPlaybackUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Peacock /watch/playback/vod/_/{streamId} URL is a submit URL, " +
        "because some episode playback links use an underscore GMO placeholder.")]
    public void playback_url_with_underscore_gmo_is_submit_url()
    {
        // Arrange
        var url = new Uri(
            "https://www.peacocktv.com/watch/playback/vod/_/99d0061c-9f02-3093-819d-c397049a8106");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsPlaybackUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Peacock /watch/playback/vod/{id} URL is a submit URL, " +
        "because playback pages resolve to a catalogue asset.")]
    public void playback_url_is_submit_url()
    {
        // Arrange
        var id = $"GMO_{_fixture.CreateAppleId()}";
        var url = new Uri($"https://www.peacocktv.com/watch/playback/vod/{id}");

        // Act / Assert
        PeacockUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        PeacockUrlMatcher.IsPlaybackUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Peacock app shell such as /watch/home is not a submit URL, " +
        "because it is not a catalogue asset.")]
    public void watch_home_is_not_submit_url()
    {
        // Arrange
        var url = new Uri("https://www.peacocktv.com/watch/home");

        // Act
        var matches = PeacockUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters peacocktv.com is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri(
            $"https://www.peacocktv.com.example.test/watch/asset/tv/{_fixture.CreateYouTubeId()}/{_fixture.CreateAppleId()}{_fixture.CreateAppleId()}");

        // Act
        var matches = PeacockUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
