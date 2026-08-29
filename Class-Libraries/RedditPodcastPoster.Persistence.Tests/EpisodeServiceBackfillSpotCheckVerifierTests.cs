using System.Text.Json;
using EpisodeServiceBackfill;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeServiceBackfillSpotCheckVerifierTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the stored document is missing, the spot-check counts not found.")]
    public void missing_stored_document_is_reported_as_not_found()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var sample = SampleFrom(podcast, episode);

        // Act
        var report = EpisodeServiceBackfillSpotCheckVerifier.Verify(
            [sample],
            new Dictionary<Guid, string>(),
            applied: true);

        // Assert
        report.Sampled.Should().Be(1);
        report.Checked.Should().Be(1);
        report.Missing.Should().Be(1);
        report.Ok.Should().Be(0);
        report.Failures.Should().ContainSingle(f => f.Reason == "not found");
    }

    [Fact(DisplayName =
        "When apply is off, a candidate that still needs backfill is ok, because dry-run does not write.")]
    public void dry_run_accepts_document_that_still_needs_backfill()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var sample = SampleFrom(podcast, episode);
        var json = LegacyNeedsBackfillJson(episode);

        // Act
        var report = EpisodeServiceBackfillSpotCheckVerifier.Verify(
            [sample],
            new Dictionary<Guid, string> { [episode.Id] = json },
            applied: false);

        // Assert
        report.Ok.Should().Be(1);
        report.Mismatch.Should().Be(0);
        report.Missing.Should().Be(0);
    }

    [Fact(DisplayName =
        "When apply is on and the stored document still needs backfill, the spot-check reports still NeedsBackfill.")]
    public void apply_spot_check_fails_when_document_still_needs_backfill()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var sample = SampleFrom(podcast, episode);
        var json = LegacyNeedsBackfillJson(episode);

        // Act
        var report = EpisodeServiceBackfillSpotCheckVerifier.Verify(
            [sample],
            new Dictionary<Guid, string> { [episode.Id] = json },
            applied: true);

        // Assert
        report.Mismatch.Should().Be(1);
        report.Ok.Should().Be(0);
        report.Failures.Should().ContainSingle(f => f.Reason == "still NeedsBackfill");
    }

    [Fact(DisplayName =
        "When apply is on and stored services and ids match the sampled patch, the spot-check is ok.")]
    public void apply_spot_check_ok_when_stored_catalog_matches_patch()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var sample = SampleFrom(podcast, episode);
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = episode.Id,
            ["podcastId"] = podcast.Id,
            ["spotifyId"] = EpisodeServicePresence.SpotifyEpisodeId(episode),
            ["ids"] = new Dictionary<string, string?> { ["spotify"] = EpisodeServicePresence.SpotifyEpisodeId(episode) },
            ["urls"] = new Dictionary<string, string?> { ["spotify"] = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!.ToString() },
            ["services"] = new Dictionary<string, object>
            {
                [ServiceKeys.Spotify] = new Dictionary<string, string?>
                {
                    ["url"] = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!.ToString()
                }
            }
        });

        // Act
        var report = EpisodeServiceBackfillSpotCheckVerifier.Verify(
            [sample],
            new Dictionary<Guid, string> { [episode.Id] = json },
            applied: true);

        // Assert
        report.Ok.Should().Be(1);
        report.Mismatch.Should().Be(0);
        report.Missing.Should().Be(0);
    }

    [Fact(DisplayName =
        "When stored id or podcastId no longer match the sampled identity, the spot-check reports id mismatch.")]
    public void spot_check_fails_when_stored_identity_changed()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var sample = SampleFrom(podcast, episode);
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = _fixture.CreateGuid(),
            ["podcastId"] = podcast.Id
        });

        // Act
        var report = EpisodeServiceBackfillSpotCheckVerifier.Verify(
            [sample],
            new Dictionary<Guid, string> { [episode.Id] = json },
            applied: true);

        // Assert
        report.Mismatch.Should().Be(1);
        report.Failures.Should().ContainSingle(f => f.Reason == "id mismatch");
    }

    private EpisodeServiceBackfillSpotCheckSample SampleFrom(Podcast podcast, Episode episode) =>
        new(
            episode.Id,
            podcast.Id,
            new Dictionary<string, EpisodeServiceLink>(StringComparer.Ordinal)
            {
                [ServiceKeys.Spotify] = new() { Url = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify) }
            },
            new EpisodeIds { Spotify = EpisodeServicePresence.SpotifyEpisodeId(episode) });

    private static string LegacyNeedsBackfillJson(Episode episode) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = episode.Id,
            ["podcastId"] = episode.PodcastId,
            ["spotifyId"] = EpisodeServicePresence.SpotifyEpisodeId(episode),
            ["urls"] = new Dictionary<string, string?> { ["spotify"] = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!.ToString() }
        });
}
