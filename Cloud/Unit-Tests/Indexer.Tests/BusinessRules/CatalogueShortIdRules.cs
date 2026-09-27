using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlShortening.Extensions;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class CatalogueShortIdRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A podcast episode short id stays the unprefixed GUID encoding, " +
        "so existing share links keep resolving.")]
    public void podcast_short_id_matches_the_unprefixed_guid()
    {
        // Arrange
        var id = _fixture.CreateGuid();

        // Act
        var encoded = CatalogueShortId.Encode(id, "Episode");
        var decoded = CatalogueShortId.TryDecode(encoded, out var roundTrip, out var kind);

        // Assert
        encoded.Should().Be(id.ToBase64());
        decoded.Should().BeTrue();
        roundTrip.Should().Be(id);
        kind.Should().BeNull();
    }

    [Theory(DisplayName =
        "Film, TV, and News short ids prepend f, t, or n before the GUID bytes, " +
        "and decode back to that kind.")]
    [InlineData(CatalogueShortId.Film, (byte)'f')]
    [InlineData(CatalogueShortId.TvShowEpisode, (byte)'t')]
    [InlineData(CatalogueShortId.NewsReport, (byte)'n')]
    public void kind_prefix_round_trips(string contentKind, byte prefix)
    {
        // Arrange
        var id = _fixture.CreateGuid();

        // Act
        var encoded = CatalogueShortId.Encode(id, contentKind);
        var decoded = CatalogueShortId.TryDecode(encoded, out var roundTrip, out var kind);
        var bytes = DecodeUrlBase64(encoded);

        // Assert
        decoded.Should().BeTrue();
        roundTrip.Should().Be(id);
        kind.Should().Be(contentKind);
        bytes[0].Should().Be(prefix);
        bytes.Should().HaveCount(17);
    }

    [Fact(DisplayName =
        "An existing unprefixed short link keeps its key, and a guid now stored as Film, TV, or News " +
        "resolves to that kind's path.")]
    public void legacy_short_link_redirects_when_the_guid_changes_kind()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        const string slug = "current-slug";
        var legacyKey = id.ToBase64();

        // Act
        var film = CatalogueShortId.MovedPlayablePath(slug, id, CatalogueShortId.Film);
        var episode = CatalogueShortId.MovedPlayablePath(slug, id, "Episode");

        // Assert
        CatalogueShortId.TryDecode(legacyKey, out var legacyId, out var legacyKind).Should().BeTrue();
        legacyId.Should().Be(id);
        legacyKind.Should().BeNull();
        film.Should().Be($"/film/{slug}/{CatalogueShortId.Encode(id, CatalogueShortId.Film)}");
        episode.Should().BeNull();
    }

    [Fact(DisplayName =
        "A film playable path keeps an apostrophe, parentheses, !, and * literal " +
        "and still percent-encodes space, +, and ?.")]
    public void film_path_encodes_the_slug_like_the_site()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        const string slug = "Director's Cut (1984)!*+?";

        // Act
        var path = CatalogueShortId.PlayablePath(slug, id, CatalogueShortId.Film);

        // Assert
        path.Should().Be(
            $"/film/Director's%20Cut%20(1984)!*%2B%3F/{CatalogueShortId.Encode(id, CatalogueShortId.Film)}");
    }

    private static byte[] DecodeUrlBase64(string shortId)
    {
        var padded = shortId.Replace('-', '/').Replace('_', '+');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
