using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Tubi.Matching;

namespace RedditPodcastPoster.Tubi.Tests.BusinessRules;

public class TubiUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Tubi movie URL with a numeric id is a submit URL, so submit can ingest a free film page.")]
    public void movie_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = TubiUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A locale-prefixed Tubi movie URL is a submit URL, because regional storefronts keep the same title id.")]
    public void locale_movie_path_is_submit_url()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var slug = _fixture.CreateYouTubeId();
        var url = new Uri($"https://tubitv.com/en-au/movies/{id}/{slug}");

        // Act
        var matches = TubiUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Tubi tv-shows URL with a numeric id is a submit URL, the same as a movie page.")]
    public void tv_show_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://tubitv.com/tv-shows/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = TubiUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Compact then expand of a locale Tubi movie URL yields https://tubitv.com/movies/{id}, so search svc round-trips without locale or slug.")]
    public void locale_movie_compacts_to_canonical_movies_id()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var url = new Uri($"https://www.tubitv.com/en-au/movies/{id}/{_fixture.CreateYouTubeId()}");

        // Act
        var compact = TubiUrlMatcher.TryCompactPayload(url);
        var expanded = TubiUrlMatcher.TryExpandPayload(compact!);

        // Assert
        compact.Should().Be($"movies/{id}");
        expanded.Should().Be(new Uri($"https://tubitv.com/movies/{id}"));
        TubiUrlMatcher.CanonicalUrl(url).Should().Be(expanded);
    }

    [Fact(DisplayName =
        "Compact then expand of a locale Tubi tv-shows URL yields https://tubitv.com/tv-shows/{id}, so search svc round-trips a series page without locale or slug.")]
    public void locale_tv_show_compacts_to_canonical_tv_shows_id()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var url = new Uri($"https://tubitv.com/en-au/tv-shows/{id}/{_fixture.CreateYouTubeId()}");

        // Act
        var compact = TubiUrlMatcher.TryCompactPayload(url);
        var expanded = TubiUrlMatcher.TryExpandPayload(compact!);

        // Assert
        compact.Should().Be($"tv-shows/{id}");
        expanded.Should().Be(new Uri($"https://tubitv.com/tv-shows/{id}"));
        TubiUrlMatcher.CanonicalUrl(url).Should().Be(expanded);
    }

    [Fact(DisplayName =
        "A Tubi /video/{id} URL is a submit URL and compact/expand round-trips as video/{id} → https://tubitv.com/video/{id}, because video is a first-class compact kind.")]
    public void video_path_is_submit_url_and_round_trips_video_id()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var url = new Uri($"https://tubitv.com/video/{id}");

        // Act
        var matches = TubiUrlMatcher.IsSubmitUrl(url);
        var compact = TubiUrlMatcher.TryCompactPayload(url);
        var expanded = TubiUrlMatcher.TryExpandPayload(compact!);

        // Assert
        matches.Should().BeTrue();
        compact.Should().Be($"video/{id}");
        expanded.Should().Be(new Uri($"https://tubitv.com/video/{id}"));
        TubiUrlMatcher.CanonicalUrl(url).Should().Be(expanded);
    }

    [Fact(DisplayName =
        "A Tubi /video/{id} URL does not share the /movies/{id} compact payload, because /video/ is not a movie alias and membership must not treat them as the same catalog row.")]
    public void video_path_is_not_the_same_catalog_row_as_movies_path()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var videoUrl = new Uri($"https://tubitv.com/video/{id}");
        var movieUrl = new Uri($"https://tubitv.com/movies/{id}");

        // Act
        var videoCompact = TubiUrlMatcher.TryCompactPayload(videoUrl);
        var movieCompact = TubiUrlMatcher.TryCompactPayload(movieUrl);

        // Assert
        videoCompact.Should().Be($"video/{id}");
        movieCompact.Should().Be($"movies/{id}");
        TubiUrlMatcher.CanonicalUrl(videoUrl).Should().NotBe(TubiUrlMatcher.CanonicalUrl(movieUrl));
    }

    [Fact(DisplayName =
        "The Tubi site root is not a submit URL, because it is marketing rather than a catalogue title.")]
    public void site_root_is_not_submit_url()
    {
        // Arrange
        var url = new Uri("https://tubitv.com/");

        // Act
        var matches = TubiUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Tubi category URL is not a submit URL, because a browse shelf is not a catalogue title.")]
    public void category_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://tubitv.com/category/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = TubiUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters tubitv.com is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://eviltubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = TubiUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Tubi movie URL with an extra path after the slug is not a submit URL, because that is not a title page.")]
    public void extra_path_after_slug_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}/extra");

        // Act
        var matches = TubiUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
