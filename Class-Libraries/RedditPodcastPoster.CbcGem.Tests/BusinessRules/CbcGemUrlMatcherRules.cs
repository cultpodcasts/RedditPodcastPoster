using System.Text.RegularExpressions;
using FluentAssertions;
using RedditPodcastPoster.CbcGem.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.CbcGem.Tests.BusinessRules;

public class CbcGemUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A CBC Gem single-slug page is a submit URL whose compact payload is the slug, " +
        "because films (and series hubs, which the extractor rejects) live at /{slug}.")]
    public void single_slug_is_submit_url()
    {
        // Arrange
        var slug = CreateSlug();
        var url = new Uri($"https://gem.cbc.ca/{slug}");

        // Act
        var matches = CbcGemUrlMatcher.IsSubmitUrl(url);
        var payload = CbcGemUrlMatcher.TryCompactPayload(url);

        // Assert
        matches.Should().BeTrue();
        payload.Should().Be(slug);
        CbcGemUrlMatcher.IsEpisodeUrl(url).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A CBC Gem /{show}/s{NN}e{NN} page is an episode submit URL, " +
        "so series episodes are accepted and kept distinct from single-slug film pages.")]
    public void season_episode_path_is_episode_submit_url()
    {
        // Arrange
        var slug = CreateSlug();
        var url = new Uri($"https://gem.cbc.ca/{slug}/s01e01");

        // Act
        var payload = CbcGemUrlMatcher.TryCompactPayload(url);

        // Assert
        payload.Should().Be($"{slug}/s01e01");
        CbcGemUrlMatcher.IsEpisodeUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A CBC Gem URL with www, upper case, a trailing slash, a query such as autoplay, or a fragment " +
        "canonicalises to https://gem.cbc.ca/{slug}, so the same film is stored and matched once.")]
    public void decorated_url_canonicalises_to_bare_slug()
    {
        // Arrange
        var slug = CreateSlug();
        var url = new Uri($"http://www.gem.cbc.ca/{slug.ToUpperInvariant()}/?autoplay=1#player");

        // Act
        var canonical = CbcGemUrlMatcher.CanonicalUrl(url);

        // Assert
        canonical.Should().Be(new Uri($"https://gem.cbc.ca/{slug}"));
    }

    [Theory(DisplayName =
        "CBC Gem roots, app shells, season hubs and deeper paths are not submit URLs, " +
        "because only /{slug} and /{slug}/s{NN}e{NN} are catalogue items.")]
    [InlineData("https://gem.cbc.ca/")]
    [InlineData("https://gem.cbc.ca/live")]
    [InlineData("https://gem.cbc.ca/search")]
    [InlineData("https://gem.cbc.ca/category/documentaries")]
    [InlineData("https://gem.cbc.ca/example-show/s01")]
    [InlineData("https://gem.cbc.ca/example-show/s01e01/extra")]
    [InlineData("https://gem.cbc.ca/example-show/trailer")]
    [InlineData("https://gem.cbc.ca/example_show")]
    public void non_catalogue_path_is_not_submit_url(string value)
    {
        // Arrange
        var url = new Uri(value);

        // Act
        var matches = CbcGemUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters gem.cbc.ca is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var slug = CreateSlug();
        var urls = new[]
        {
            new Uri($"https://gem.cbc.ca.example.test/{slug}"),
            new Uri($"https://evilgem.cbc.ca/{slug}"),
            new Uri($"https://www.cbc.ca/{slug}")
        };

        // Act
        var matches = urls.Select(CbcGemUrlMatcher.IsSubmitUrl).ToArray();

        // Assert
        matches.Should().AllBeEquivalentTo(false);
    }

    [Fact(DisplayName =
        "A CBC Gem compact payload expands back to the canonical https URL, and a payload that is not a " +
        "catalogue path does not expand, so search svc tokens round-trip without inventing URLs.")]
    public void compact_payload_round_trips()
    {
        // Arrange
        var film = CreateSlug();
        var episode = $"{CreateSlug()}/s02e10";

        // Act
        var filmUrl = CbcGemUrlMatcher.TryExpandPayload(film);
        var episodeUrl = CbcGemUrlMatcher.TryExpandPayload(episode);
        var shell = CbcGemUrlMatcher.TryExpandPayload("live");

        // Assert
        filmUrl.Should().Be(new Uri($"https://gem.cbc.ca/{film}"));
        episodeUrl.Should().Be(new Uri($"https://gem.cbc.ca/{episode}"));
        shell.Should().BeNull();
    }

    private string CreateSlug() =>
        Regex.Replace(_fixture.CreateTitle().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
}
