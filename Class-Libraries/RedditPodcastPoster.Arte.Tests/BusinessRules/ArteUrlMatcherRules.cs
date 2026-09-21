using FluentAssertions;
using RedditPodcastPoster.Arte.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Arte.Tests.BusinessRules;

public class ArteUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An ARTE collection URL /{language}/videos/RC-{id}/{slug}/ is a submit URL for every two-letter catalogue language, " +
        "so French is not the only language that can be ingested.")]
    public void collection_url_accepts_every_two_letter_language()
    {
        // Arrange
        var languages = new[] { "fr", "de", "en", "es", "pl", "it", "ro", "zz" };

        // Act / Assert
        foreach (var language in languages)
        {
            var url = CollectionUrl(language);
            ArteUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
            ArteUrlMatcher.IsSeriesUrl(url).Should().BeTrue();
            ArteUrlMatcher.IsEpisodeUrl(url).Should().BeFalse();
        }
    }

    [Fact(DisplayName =
        "An ARTE programme URL /{language}/videos/{digits}-{digits}-{letter}/{slug}/ is an episode submit URL, " +
        "including when the slug is omitted.")]
    public void programme_url_is_episode_submit_url()
    {
        // Arrange
        var withSlug = ProgrammeUrl("en", includeSlug: true);
        var withoutSlug = ProgrammeUrl("de", includeSlug: false);

        // Act / Assert
        ArteUrlMatcher.IsSubmitUrl(withSlug).Should().BeTrue();
        ArteUrlMatcher.IsEpisodeUrl(withSlug).Should().BeTrue();
        ArteUrlMatcher.IsSeriesUrl(withSlug).Should().BeFalse();
        ArteUrlMatcher.IsSubmitUrl(withoutSlug).Should().BeTrue();
        ArteUrlMatcher.IsEpisodeUrl(withoutSlug).Should().BeTrue();
    }

    [Fact(DisplayName =
        "An ARTE theme shelf such as /{language}/videos/{shelf}/ is not a submit URL, " +
        "because it is a browse category rather than a collection or programme.")]
    public void theme_shelf_is_not_submit_url()
    {
        // Arrange
        var language = "fr";
        var shelf = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.arte.tv/{language}/videos/{shelf}/");

        // Act
        var matches = ArteUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "An ARTE language home or a three-letter language segment is not a submit URL, " +
        "because catalogue items live under a two-letter /videos/ path.")]
    public void language_home_and_long_language_are_not_submit_urls()
    {
        // Arrange
        var home = new Uri("https://www.arte.tv/en/");
        var longLanguage = ProgrammeUrl("eng", includeSlug: true);

        // Act / Assert
        ArteUrlMatcher.IsSubmitUrl(home).Should().BeFalse();
        ArteUrlMatcher.IsSubmitUrl(longLanguage).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host or an arte.tv subdomain is not a submit URL, " +
        "because only the apex catalogue host is ingested.")]
    public void lookalike_and_subdomain_are_not_submit_urls()
    {
        // Arrange
        var lookalike = new Uri(
            $"https://www.arte.tv.example.test/fr/videos/RC-{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}/");
        var subdomain = new Uri(
            $"https://api.arte.tv/fr/videos/RC-{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}/");

        // Act / Assert
        ArteUrlMatcher.IsSubmitUrl(lookalike).Should().BeFalse();
        ArteUrlMatcher.IsSubmitUrl(subdomain).Should().BeFalse();
    }

    private Uri CollectionUrl(string language)
    {
        var id = _fixture.CreateAppleId();
        var slug = _fixture.CreateYouTubeId();
        return new Uri($"https://arte.tv/{language}/videos/RC-{id}/{slug}/");
    }

    private Uri ProgrammeUrl(string language, bool includeSlug)
    {
        var head = _fixture.CreateAppleId().ToString();
        var tail = _fixture.CreateAppleId().ToString();
        var id = $"{head[..6]}-{tail[..3]}-A";
        var slug = includeSlug ? $"/{_fixture.CreateYouTubeId()}" : string.Empty;
        return new Uri($"https://www.arte.tv/{language}/videos/{id}{slug}");
    }
}
