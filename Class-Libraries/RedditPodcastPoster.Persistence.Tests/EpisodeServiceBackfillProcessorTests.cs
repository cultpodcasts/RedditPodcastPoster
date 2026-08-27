// pragma: allowlist secret
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using RedditPodcastPoster.Persistence.Abstractions.Repositories; // pragma: allowlist secret
using RedditPodcastPoster.Persistence.Episodes; // pragma: allowlist secret
using Xunit;

namespace RedditPodcastPoster.Persistence.Tests; // pragma: allowlist secret

public class EpisodeServiceBackfillProcessorTests // pragma: allowlist secret
{
    [Fact(DisplayName =
        "Episode service backfill dry-run: when raw documents include a coverage gap, then the report counts the candidate and does not load or save, because apply is off.")] // pragma: allowlist secret
    public async Task dry_run_counts_candidates_without_saving()
    {
        // Arrange
        var episodeId = Guid.NewGuid(); // pragma: allowlist secret
        var podcastId = Guid.NewGuid(); // pragma: allowlist secret
        var repo = new Mock<IEpisodeRepository>(MockBehavior.Strict); // pragma: allowlist secret
        var sut = new EpisodeServiceBackfillProcessor(repo.Object, NullLogger<EpisodeServiceBackfillProcessor>.Instance); // pragma: allowlist secret
        var json = $$"""
            {
              "id": "{{episodeId}}",
              "podcastId": "{{podcastId}}",
              "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk"
            }
            """; // pragma: allowlist secret

        // Act
        var report = await sut.RunAsync([json], apply: false);

        // Assert
        report.Candidates.Should().Be(1);
        report.Saved.Should().Be(0);
        report.Applied.Should().BeFalse();
        repo.Verify(x => x.GetEpisode(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never); // pragma: allowlist secret
        repo.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "Episode service backfill apply: when a loaded episode still has only a legacy Spotify url, then Save persists services and nested ids.")] // pragma: allowlist secret
    public async Task apply_saves_episode_when_legacy_shape_changes() // pragma: allowlist secret
    {
        // Arrange
        var episodeId = Guid.NewGuid(); // pragma: allowlist secret
        var podcastId = Guid.NewGuid(); // pragma: allowlist secret
        var episode = new Episode // pragma: allowlist secret
        {
            Id = episodeId, // pragma: allowlist secret
            PodcastId = podcastId, // pragma: allowlist secret
            SpotifyId = "4rOoJ6Egrf8K2IrywzwOMk",
            Urls = new ServiceUrls
            {
                Spotify = new Uri("https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk") // pragma: allowlist secret
            }
        };
        var repo = new Mock<IEpisodeRepository>(); // pragma: allowlist secret
        repo.Setup(x => x.GetEpisode(podcastId, episodeId)).ReturnsAsync(episode); // pragma: allowlist secret
        repo.Setup(x => x.Save(episode)).Returns(Task.CompletedTask); // pragma: allowlist secret
        var sut = new EpisodeServiceBackfillProcessor(repo.Object, NullLogger<EpisodeServiceBackfillProcessor>.Instance); // pragma: allowlist secret
        var json = $$"""
            {
              "id": "{{episodeId}}",
              "podcastId": "{{podcastId}}",
              "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk",
              "urls": { "spotify": "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk" }
            }
            """; // pragma: allowlist secret

        // Act
        var report = await sut.RunAsync([json], apply: true);

        // Assert
        report.Applied.Should().BeTrue();
        report.Saved.Should().Be(1);
        report.Missing.Should().Be(0);
        episode.Services.Should().ContainKey(ServiceKeys.Spotify); // pragma: allowlist secret
        episode.Ids!.Spotify.Should().Be("4rOoJ6Egrf8K2IrywzwOMk"); // pragma: allowlist secret
        repo.Verify(x => x.Save(episode), Times.Once); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "Episode service backfill apply: when the episode id is selected from raw JSON but GetEpisode returns null, then Save is not called and Missing increments.")] // pragma: allowlist secret
    public async Task apply_skips_save_when_episode_missing() // pragma: allowlist secret
    {
        // Arrange
        var episodeId = Guid.NewGuid(); // pragma: allowlist secret
        var podcastId = Guid.NewGuid(); // pragma: allowlist secret
        var repo = new Mock<IEpisodeRepository>(); // pragma: allowlist secret
        repo.Setup(x => x.GetEpisode(podcastId, episodeId)).ReturnsAsync((Episode?)null); // pragma: allowlist secret
        var sut = new EpisodeServiceBackfillProcessor(repo.Object, NullLogger<EpisodeServiceBackfillProcessor>.Instance); // pragma: allowlist secret
        var json = $$"""
            {
              "id": "{{episodeId}}",
              "podcastId": "{{podcastId}}",
              "spotifyId": "4rOoJ6Egrf8K2IrywzwOMk"
            }
            """; // pragma: allowlist secret

        // Act
        var report = await sut.RunAsync([json], apply: true);

        // Assert
        report.Missing.Should().Be(1);
        report.Saved.Should().Be(0);
        repo.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never); // pragma: allowlist secret
    }
}
