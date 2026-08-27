// pragma: allowlist secret
using FluentAssertions; // pragma: allowlist secret
using RedditPodcastPoster.EntitySearchIndexer.Models; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using Xunit; // pragma: allowlist secret

namespace Indexer.Tests; // pragma: allowlist secret

public class SearchEpisodeServicesTests // pragma: allowlist secret
{
    [Fact(DisplayName =
        "Search svc encoding stores BBC Sounds as a compact play-id when the URL is the standard sounds/play shape, because the index must stay small while remaining loss-less.")]
    public void Compacts_bbc_sounds_play_url_to_id()
    {
        // Arrange
        var services = new Dictionary<string, EpisodeServiceLink> // pragma: allowlist secret
        {
            [ServiceKeys.BbcSounds] = new()
            {
                Url = new Uri("https://www.bbc.co.uk/sounds/play/p0example")
            }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services); // pragma: allowlist secret
        var expanded = SearchEpisodeServices.Expand(compact); // pragma: allowlist secret

        // Assert
        compact.Should().Be("bbcSounds:p0example");
        expanded.Should().ContainSingle()
            .Which.Url.ToString().Should().Be("https://www.bbc.co.uk/sounds/play/p0example");
    }

    [Fact(DisplayName =
        "Search svc encoding stores Vimeo as a numeric id when the URL is vimeo.com/{id}, so Vimeo uses the same compact grammar as BBC and Archive.")]
    public void Compacts_vimeo_watch_url_to_id()
    {
        // Arrange
        var services = new Dictionary<string, EpisodeServiceLink> // pragma: allowlist secret
        {
            [ServiceKeys.Vimeo] = new() { Url = new Uri("https://vimeo.com/123456789") }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services); // pragma: allowlist secret
        var expanded = SearchEpisodeServices.Expand(compact); // pragma: allowlist secret

        // Assert
        compact.Should().Be("vimeo:123456789");
        expanded.Should().ContainSingle()
            .Which.Url.ToString().Should().Be("https://vimeo.com/123456789");
    }

    [Fact(DisplayName =
        "Search svc encoding omits Spotify/YouTube/Apple because those URLs are rebuilt from index id fields, keeping quota for services that are not id-derivable.")]
    public void Omits_reconstructable_platform_ids()
    {
        // Arrange
        var services = new Dictionary<string, EpisodeServiceLink> // pragma: allowlist secret
        {
            [ServiceKeys.Spotify] = new() { Url = new Uri("https://open.spotify.com/episode/opaqueid00000000000000") },
            [ServiceKeys.YouTube] = new() { Url = new Uri("https://www.youtube.com/watch?v=griffinsong42") },
            [ServiceKeys.InternetArchive] = new() { Url = new Uri("https://archive.org/details/harbour-vale-ep") }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services); // pragma: allowlist secret

        // Assert
        compact.Should().Be("internetArchive:harbour-vale-ep");
    }

    [Fact(DisplayName =
        "Search svc encoding keeps a full Netflix title URL when it cannot be compacted without changing the original string, so documentary links are never lossy.")]
    public void Keeps_nonstandard_netflix_url_as_full_payload()
    {
        // Arrange
        var url = new Uri("https://www.netflix.com/watch/81040344?trackId=14262865");
        var services = new Dictionary<string, EpisodeServiceLink> // pragma: allowlist secret
        {
            [ServiceKeys.Netflix] = new() { Url = url }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services); // pragma: allowlist secret
        var expanded = SearchEpisodeServices.Expand(compact); // pragma: allowlist secret

        // Assert
        compact.Should().StartWith("netflix:uhttps://");
        expanded.Should().ContainSingle().Which.Url.Should().Be(url);
    }
}
