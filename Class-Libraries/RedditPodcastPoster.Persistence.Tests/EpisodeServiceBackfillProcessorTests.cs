// pragma: allowlist secret
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using RedditPodcastPoster.Persistence.Abstractions.Repositories; // pragma: allowlist secret
using RedditPodcastPoster.Persistence.Episodes; // pragma: allowlist secret
using Xunit;

namespace RedditPodcastPoster.Persistence.Tests; // pragma: allowlist secret

public class EpisodeServiceBackfillProcessorTests // pragma: allowlist secret
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker;
    private readonly Mock<IEpisodeRepository> _repository;

    public EpisodeServiceBackfillProcessorTests()
    {
        _mocker = new AutoMocker();
        _repository = _mocker.GetMock<IEpisodeRepository>();
        _mocker.Use(NullLogger<EpisodeServiceBackfillProcessor>.Instance);
    }

    [Fact(DisplayName =
        "Episode service backfill dry-run: when raw documents include a coverage gap, then the report counts the candidate and does not load or save, because apply is off.")] // pragma: allowlist secret
    public async Task dry_run_counts_candidates_without_saving()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var json = $$"""
            {
              "id": "{{episode.Id}}",
              "podcastId": "{{podcast.Id}}",
              "spotifyId": "{{episode.SpotifyId}}"
            }
            """; // pragma: allowlist secret
        var sut = _mocker.CreateInstance<EpisodeServiceBackfillProcessor>();

        // Act
        var report = await sut.RunAsync([json], apply: false);

        // Assert
        report.Candidates.Should().Be(1);
        report.Saved.Should().Be(0);
        report.Applied.Should().BeFalse();
        _repository.Verify(x => x.GetEpisode(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never); // pragma: allowlist secret
        _repository.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never); // pragma: allowlist secret
        _repository.Verify(
            x => x.PatchServicesAndIds(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, EpisodeServiceLink>?>(),
                It.IsAny<EpisodeIds?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Episode service backfill apply: when raw JSON has only a legacy Spotify id, then a services/ids patch is persisted and Save is not called.")] // pragma: allowlist secret
    public async Task apply_patches_episode_when_legacy_shape_needs_catalog() // pragma: allowlist secret
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var json = $$"""
            {
              "id": "{{episode.Id}}",
              "podcastId": "{{podcast.Id}}",
              "spotifyId": "{{episode.SpotifyId}}",
              "urls": { "spotify": "{{episode.Urls.Spotify}}" }
            }
            """; // pragma: allowlist secret
        _repository
            .Setup(x => x.PatchServicesAndIds(
                podcast.Id,
                episode.Id,
                It.IsAny<Dictionary<string, EpisodeServiceLink>?>(),
                It.IsAny<EpisodeIds?>()))
            .ReturnsAsync(true);
        var sut = _mocker.CreateInstance<EpisodeServiceBackfillProcessor>();

        // Act
        var report = await sut.RunAsync([json], apply: true);

        // Assert
        report.Applied.Should().BeTrue();
        report.Saved.Should().Be(1);
        report.Missing.Should().Be(0);
        var nestedSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode);
        _repository.Verify(
            x => x.PatchServicesAndIds(
                podcast.Id,
                episode.Id,
                It.Is<Dictionary<string, EpisodeServiceLink>?>(s =>
                    s != null && s.ContainsKey(ServiceKeys.Spotify)),
                It.Is<EpisodeIds?>(ids => ids != null && ids.Spotify == nestedSpotifyId)),
            Times.Once);
        _repository.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "Episode service backfill apply: when the episode id is selected from raw JSON but the item is missing, then Save is not called and Missing increments.")] // pragma: allowlist secret
    public async Task apply_skips_save_when_episode_missing() // pragma: allowlist secret
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var json = $$"""
            {
              "id": "{{episode.Id}}",
              "podcastId": "{{podcast.Id}}",
              "spotifyId": "{{episode.SpotifyId}}"
            }
            """; // pragma: allowlist secret
        _repository
            .Setup(x => x.PatchServicesAndIds(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, EpisodeServiceLink>?>(),
                It.IsAny<EpisodeIds?>()))
            .ReturnsAsync(false);
        var sut = _mocker.CreateInstance<EpisodeServiceBackfillProcessor>();

        // Act
        var report = await sut.RunAsync([json], apply: true);

        // Assert
        report.Missing.Should().Be(1);
        report.Saved.Should().Be(0);
        _repository.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "When the patch identity does not match the source JSON document, apply refuses and does not call PatchServicesAndIds, because a mismatched patch would write the wrong item.")]
    public async Task apply_refuses_when_patch_identity_does_not_match_document()
    {
        // Arrange
        var sourcePodcast = _fixture.CreatePodcast();
        var sourceEpisode = _fixture.CreateStoredEpisodeWithSpotifyOnly(sourcePodcast);
        var sourceJson = LegacyJson(sourceEpisode);
        var otherPodcast = _fixture.CreatePodcast();
        var otherEpisode = _fixture.CreateStoredEpisodeWithSpotifyOnly(otherPodcast);
        EpisodeServiceCatalogPatchFactory.TryCreate(LegacyJson(otherEpisode), out var otherPatch)
            .Should().BeTrue();
        var sut = _mocker.CreateInstance<EpisodeServiceBackfillProcessor>();

        // Act
        var written = await sut.ApplyPatchAsync(sourceJson, otherPatch!);

        // Assert
        written.Should().BeFalse();
        _repository.Verify(
            x => x.PatchServicesAndIds(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, EpisodeServiceLink>?>(),
                It.IsAny<EpisodeIds?>()),
            Times.Never);
        _repository.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "When apply runs in parallel, each PatchServicesAndIds call uses the podcast and episode ids from that document, because patches must not be mixed across work items.")]
    public async Task parallel_apply_patches_each_document_with_matching_ids()
    {
        // Arrange
        var documents = new List<(Podcast Podcast, Episode Episode, string Json)>();
        for (var i = 0; i < 8; i++)
        {
            var podcast = _fixture.CreatePodcast();
            var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
            documents.Add((podcast, episode, LegacyJson(episode)));
        }

        var patched = new System.Collections.Concurrent.ConcurrentBag<(Guid PodcastId, Guid EpisodeId)>();
        _repository
            .Setup(x => x.PatchServicesAndIds(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, EpisodeServiceLink>?>(),
                It.IsAny<EpisodeIds?>()))
            .Callback<Guid, Guid, Dictionary<string, EpisodeServiceLink>?, EpisodeIds?>(
                (podcastId, episodeId, _, _) => patched.Add((podcastId, episodeId)))
            .ReturnsAsync(true);
        var sut = _mocker.CreateInstance<EpisodeServiceBackfillProcessor>();

        // Act
        var report = await sut.RunAsync(
            documents.Select(d => d.Json).ToList(),
            apply: true,
            maxDegreeOfParallelism: 4);

        // Assert
        report.Saved.Should().Be(documents.Count);
        report.Mismatches.Should().Be(0);
        patched.Should().BeEquivalentTo(documents.Select(d => (d.Podcast.Id, d.Episode.Id)));
        _repository.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    private static string LegacyJson(Episode episode) =>
        $$"""
          {
            "id": "{{episode.Id}}",
            "podcastId": "{{episode.PodcastId}}",
            "spotifyId": "{{episode.SpotifyId}}",
            "urls": { "spotify": "{{episode.Urls.Spotify}}" }
          }
          """;
}
