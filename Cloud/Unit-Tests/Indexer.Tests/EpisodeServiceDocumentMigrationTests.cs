// pragma: allowlist secret
using System.Text.Json; // pragma: allowlist secret
using FluentAssertions; // pragma: allowlist secret
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using Xunit; // pragma: allowlist secret

namespace Indexer.Tests; // pragma: allowlist secret

public class EpisodeServiceDocumentMigrationTests // pragma: allowlist secret
{
    [Fact(DisplayName =
        "NeedsBackfill is false for a document that already has services and ids covering every legacy url and top-level id, so a complete dual-write document is not selected again.")] // pragma: allowlist secret
    public void needs_backfill_false_when_services_and_ids_cover_legacy_slots() // pragma: allowlist secret
    {
        // Arrange
        var json =
            """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "podcastId": "22222222-2222-2222-2222-222222222222",
              "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk",
              "ids": { "spotify": "4rOoJ6Egrf8K2IrywzwOMk" },
              "urls": { "spotify": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk" },
              "services": { "spotify": { "url": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk" } }
            }
            """; // pragma: allowlist secret

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement); // pragma: allowlist secret

        // Assert
        needs.Should().BeFalse();
    }

    [Fact(DisplayName =
        "NeedsBackfill is true when urls.spotify is present and services.spotify is missing, because the adjacent services map is the persisted catalog shape.")]
    public void needs_backfill_true_when_legacy_spotify_url_has_no_service()
    {
        // Arrange
        var json =
            """
            {
              "urls": { "spotify": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk" }
            }
            """; // pragma: allowlist secret

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement); // pragma: allowlist secret

        // Assert
        needs.Should().BeTrue();
    }

    [Fact(DisplayName =
        "NeedsBackfill is true when a top-level Spotify id is present and nested ids.spotify is missing, because presence of that service is the nested id.")] // pragma: allowlist secret
    public void needs_backfill_true_when_top_level_spotify_id_has_no_nested_id()
    {
        // Arrange
        var json = """{ "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk" }"""; // pragma: allowlist secret

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement); // pragma: allowlist secret

        // Assert
        needs.Should().BeTrue();
    }

    [Fact(DisplayName =
        "NeedsBackfill is true when urls.bbc is an iPlayer page and services.bbcIplayer is missing, because Sounds and iPlayer are distinct keys.")]
    public void needs_backfill_true_when_iplayer_url_has_no_bbc_iplayer_service()
    {
        // Arrange
        var json =
            """
            {
              "urls": { "bbc": "https://www.bbc.co.uk/iplayer/episode/p0abcd12" }
            }
            """; // pragma: allowlist secret

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement); // pragma: allowlist secret

        // Assert
        needs.Should().BeTrue();
    }

    [Fact(DisplayName =
        "NeedsBackfill is false when the document has no urls, ids, or platform ids, so empty episodes are not written.")] // pragma: allowlist secret
    public void needs_backfill_false_for_empty_document()
    {
        // Arrange
        var json = """{ "title": "Untitled" }""";

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement); // pragma: allowlist secret

        // Assert
        needs.Should().BeFalse();
    }

    [Fact(DisplayName =
        "SelectDocumentsToBackfill returns only episode refs whose raw JSON has coverage gaps, so a dry-run list is the apply set.")] // pragma: allowlist secret
    public void select_documents_returns_only_gap_refs() // pragma: allowlist secret
    {
        // Arrange
        var gapEpisodeId = Guid.NewGuid(); // pragma: allowlist secret
        var completeEpisodeId = Guid.NewGuid(); // pragma: allowlist secret
        var podcastId = Guid.NewGuid(); // pragma: allowlist secret
        var gap = $$"""
            {
              "id": "{{gapEpisodeId}}",
              "podcastId": "{{podcastId}}",
              "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk"
            }
            """; // pragma: allowlist secret
        var complete = $$"""
            {
              "id": "{{completeEpisodeId}}",
              "podcastId": "{{podcastId}}",
              "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk",
              "ids": { "spotify": "4rOoJ6Egrf8K2IrywzwOMk" },
              "urls": { "spotify": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk" },
              "services": { "spotify": { "url": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk" } }
            }
            """; // pragma: allowlist secret

        // Act
        var selected = EpisodeServiceDocumentMigration.SelectDocumentsToBackfill([gap, complete]); // pragma: allowlist secret

        // Assert
        selected.Should().ContainSingle()
            .Which.Should().Be(new EpisodeServiceDocumentMigration.EpisodeRef(podcastId, gapEpisodeId)); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "MergeRawLeftoverIntoCatalog writes services and nested ids from leftover JSON urls and top-level ids, and a second Apply is unchanged so backfill is idempotent.")] // pragma: allowlist secret
    public void merge_raw_leftover_hydrates_catalog_and_apply_is_idempotent()
    {
        // Arrange
        var leftover =
            """
            {
              "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk",
              "urls": {
                "spotify": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk",
                "bbc": "https://www.bbc.co.uk/iplayer/episode/p0abcd12"
              },
              "images": { "other": "https://ichef.bbci.co.uk/images/ic/1200x675/p0artwork.jpg" }
            }
            """; // pragma: allowlist secret
        var episode = new Episode(); // pragma: allowlist secret
        using var document = JsonDocument.Parse(leftover); // pragma: allowlist secret

        // Act
        EpisodeServiceDocumentMigration.MergeRawLeftoverIntoCatalog(episode, document.RootElement); // pragma: allowlist secret
        EpisodeServiceDocumentMigration.Apply(episode); // pragma: allowlist secret
        var second = EpisodeServiceDocumentMigration.Apply(episode); // pragma: allowlist secret

        // Assert
        episode.Services.Should().ContainKey(ServiceKeys.Spotify); // pragma: allowlist secret
        episode.Services.Should().ContainKey(ServiceKeys.BbcIplayer); // pragma: allowlist secret
        episode.Ids!.Spotify.Should().Be("4rOoJ6Egrf8K2IrywzwOMk"); // pragma: allowlist secret
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify).Should().NotBeNull(); // pragma: allowlist secret
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer).Should().NotBeNull(); // pragma: allowlist secret
        second.Should().BeFalse();
    }
}
