using System.Text.Json;
using EpisodeServiceBackfill;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace EpisodeServiceBackfill.Tests;

public class EpisodeServiceDocumentMigrationTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "NeedsBackfill is false for a document that already has services and ids covering every leftover url and top-level id, so a complete dual-write document is not selected again.")]
    public void needs_backfill_false_when_services_and_ids_cover_legacy_slots()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var spotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode);
        var spotifyUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify);
        var json =
            $$"""
            {
              "id": "{{episode.Id}}",
              "podcastId": "{{podcast.Id}}",
              "spotifyId": "{{spotifyId}}",
              "ids": { "spotify": "{{spotifyId}}" },
              "urls": { "spotify": "{{spotifyUrl}}" },
              "services": { "spotify": { "url": "{{spotifyUrl}}" } }
            }
            """;

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement);

        // Assert
        needs.Should().BeFalse();
    }

    [Fact(DisplayName =
        "NeedsBackfill is true when urls.spotify is present and services.spotify is missing, because the adjacent services map is the persisted catalog shape.")]
    public void needs_backfill_true_when_legacy_spotify_url_has_no_service()
    {
        // Arrange
        var spotifyUrl = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var json =
            $$"""
            {
              "urls": { "spotify": "{{spotifyUrl}}" }
            }
            """;

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement);

        // Assert
        needs.Should().BeTrue();
    }

    [Fact(DisplayName =
        "NeedsBackfill is true when a top-level Spotify id is present and nested ids.spotify is missing, because presence of that service is the nested id.")]
    public void needs_backfill_true_when_top_level_spotify_id_has_no_nested_id()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var json = $$"""{ "spotifyId": "{{spotifyId}}" }""";

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement);

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
            """;

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement);

        // Assert
        needs.Should().BeTrue();
    }

    [Fact(DisplayName =
        "NeedsBackfill is false when the document has no urls, ids, or platform ids, so empty episodes are not written.")]
    public void needs_backfill_false_for_empty_document()
    {
        // Arrange
        var json = $$"""{ "title": "{{_fixture.CreateTitle()}}" }""";

        // Act
        var needs = EpisodeServiceDocumentMigration.NeedsBackfill(JsonDocument.Parse(json).RootElement);

        // Assert
        needs.Should().BeFalse();
    }

    [Fact(DisplayName =
        "SelectDocumentsToBackfill returns only episode refs whose raw JSON has coverage gaps, so a dry-run list is the apply set.")]
    public void select_documents_returns_only_gap_refs()
    {
        // Arrange
        var gapEpisodeId = _fixture.CreateGuid();
        var completeEpisodeId = _fixture.CreateGuid();
        var podcastId = _fixture.CreateGuid();
        var spotifyId = _fixture.CreateSpotifyId();
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyId);
        var gap = $$"""
            {
              "id": "{{gapEpisodeId}}",
              "podcastId": "{{podcastId}}",
              "spotifyId": "{{spotifyId}}"
            }
            """;
        var complete = $$"""
            {
              "id": "{{completeEpisodeId}}",
              "podcastId": "{{podcastId}}",
              "spotifyId": "{{spotifyId}}",
              "ids": { "spotify": "{{spotifyId}}" },
              "urls": { "spotify": "{{spotifyUrl}}" },
              "services": { "spotify": { "url": "{{spotifyUrl}}" } }
            }
            """;

        // Act
        var selected = EpisodeServiceDocumentMigration.SelectDocumentsToBackfill([gap, complete]);

        // Assert
        selected.Should().ContainSingle()
            .Which.Should().Be(new EpisodeServiceDocumentMigration.EpisodeRef(podcastId, gapEpisodeId));
    }

    [Fact(DisplayName =
        "MergeRawLeftoverIntoCatalog writes services and nested ids from leftover JSON urls and top-level ids, and a second Apply is unchanged so backfill is idempotent.")]
    public void merge_raw_leftover_hydrates_catalog_and_apply_is_idempotent()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyId);
        var leftover =
            $$"""
            {
              "spotifyId": "{{spotifyId}}",
              "urls": {
                "spotify": "{{spotifyUrl}}",
                "bbc": "https://www.bbc.co.uk/iplayer/episode/p0abcd12"
              },
              "images": { "other": "https://cdn.example.test/artwork.jpg" }
            }
            """;
        var episode = _fixture.CreateEpisode();
        using var document = JsonDocument.Parse(leftover);

        // Act
        EpisodeServiceDocumentMigration.MergeRawLeftoverIntoCatalog(episode, document.RootElement);
        EpisodeServiceDocumentMigration.Apply(episode);
        var second = EpisodeServiceDocumentMigration.Apply(episode);

        // Assert
        episode.Services.Should().ContainKey(ServiceKeys.Spotify);
        episode.Services.Should().ContainKey(ServiceKeys.BbcIplayer);
        episode.Ids!.Spotify.Should().Be(spotifyId);
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify).Should().Be(spotifyUrl);
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer).Should().NotBeNull();
        second.Should().BeFalse();
    }
}
