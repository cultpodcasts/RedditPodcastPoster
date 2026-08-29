using EpisodeServiceBackfill;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace EpisodeServiceBackfill.Tests;

public class LeftoverEpisodeDocumentTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When leftover JSON has Spotify url and top-level id but no catalog services, " +
        "the CLI leftover document needs backfill and builds a services/ids patch.")]
    public void leftover_document_reads_retired_members_and_builds_catalog_patch()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var spotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode);
        var spotifyUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify);
        var json = $$"""
            {
              "id": "{{episode.Id}}",
              "podcastId": "{{podcast.Id}}",
              "spotifyId": "{{spotifyId}}",
              "urls": { "spotify": "{{spotifyUrl}}" }
            }
            """;

        // Act
        var parsed = LeftoverEpisodeDocument.TryParse(json, out var leftover);
        var needs = leftover!.NeedsBackfill();
        var created = leftover.TryCreateCatalogPatch(out var patch);

        // Assert
        parsed.Should().BeTrue();
        leftover.SpotifyId.Should().Be(spotifyId);
        leftover.Urls!.Spotify.Should().Be(spotifyUrl);
        leftover.Id.Should().Be(episode.Id);
        leftover.PodcastId.Should().Be(podcast.Id);
        needs.Should().BeTrue();
        created.Should().BeTrue();
        patch!.Ids!.Spotify.Should().Be(spotifyId);
        patch.Services.Should().ContainKey(ServiceKeys.Spotify);
        LeftoverEpisodeDocument.Classify(json).Should().BeNull();
    }

    [Fact(DisplayName =
        "When leftover JSON already has nested ids and services covering Spotify, NeedsBackfill is false.")]
    public void leftover_document_does_not_need_backfill_when_catalog_covers_spotify()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var spotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode);
        var spotifyUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify);
        var json = $$"""
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
        LeftoverEpisodeDocument.TryParse(json, out var leftover);

        // Assert
        leftover!.NeedsBackfill().Should().BeFalse();
        leftover.TryCreateCatalogPatch(out _).Should().BeFalse();
    }
}
