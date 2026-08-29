using System.Text.Json;
using EpisodeServiceBackfill;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Tests.Fakes;
using Xunit;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeServiceCatalogPatchTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker;
    private readonly Mock<IBackfillEpisodeRepository> _repository;

    public EpisodeServiceCatalogPatchTests()
    {
        _mocker = new AutoMocker();
        _repository = _mocker.GetMock<IBackfillEpisodeRepository>();
        _mocker.Use(NullLogger<EpisodeServiceBackfillProcessor>.Instance);
        _mocker.Use<IEpisodeCatalogPatchSource>(new JsonEpisodeCatalogPatchSource());
    }

    [Fact(DisplayName =
        "When stored JSON has Spotify url and top-level Spotify id but no services or nested ids, " +
        "the catalog patch adds services.spotify and ids.spotify and does not carry urls, title, or lang.")]
    public void patch_from_spotify_legacy_shape_is_additive_catalog_only()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var extra = _fixture.Create<string>();
        var json = ToLegacyJson(episode, extra);

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch.Should().NotBeNull();
        patch!.EpisodeId.Should().Be(episode.Id);
        patch.PodcastId.Should().Be(podcast.Id);
        patch.Services.Should().ContainKey(ServiceKeys.Spotify);
        patch.Services![ServiceKeys.Spotify].Url.Should().Be(EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify));
        patch.Ids!.Spotify.Should().Be(EpisodeServicePresence.SpotifyEpisodeId(episode));
        patch.Ids.YouTube.Should().BeNull();
        JsonSerializer.Serialize(patch).Should().NotContain(episode.Title);
        JsonSerializer.Serialize(patch).Should().NotContain("\"lang\"");
        JsonSerializer.Serialize(patch).Should().NotContain(extra);
        JsonSerializer.Serialize(patch).Should().NotContain("urls");
    }

    [Fact(DisplayName =
        "When stored JSON is YouTube-only with artwork, the catalog patch adds services.youtube url and image " +
        "and nested ids.youtube, because that is the reconstructable YouTube identity.")]
    public void patch_from_youtube_legacy_shape_includes_image()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode);
        var art = new Uri($"https://i.ytimg.com/vi/{youTubeId}/maxresdefault.jpg");
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.YouTube, art);
        var json = ToLegacyJson(episode);

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch!.Services.Should().ContainKey(ServiceKeys.YouTube);
        patch.Services![ServiceKeys.YouTube].Url.Should().Be(EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube));
        patch.Services[ServiceKeys.YouTube].Image.Should().Be(art);
        patch.Ids!.YouTube.Should().Be(youTubeId);
        patch.Ids.Spotify.Should().BeNull();
    }

    [Fact(DisplayName =
        "When stored JSON has Apple url and top-level appleId but no nested ids, " +
        "the catalog patch adds services.apple and ids.apple.")]
    public void patch_from_apple_legacy_shape_adds_nested_apple_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var appleId = _fixture.CreateAppleId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.SetAppleIdentity(e, appleId);
            EpisodeServicePresence.Upsert(e, ServiceKeys.Apple, _fixture.DefaultAppleUrl(appleId), null);
            EpisodeServicePresence.SetSpotifyIdentity(e, null);
            EpisodeServicePresence.SetYouTubeIdentity(e, null);
        });
        var json = ToLegacyJson(episode);

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch!.Services.Should().ContainKey(ServiceKeys.Apple);
        patch.Services![ServiceKeys.Apple].Url.Should().Be(EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple));
        patch.Ids!.Apple.Should().Be(appleId);
    }

    [Fact(DisplayName =
        "When stored JSON has urls.bbc as an iPlayer episode page and services.bbcIplayer is missing, " +
        "the catalog patch uses the iPlayer key, not Sounds, because path distinguishes the two BBC products.")]
    public void patch_from_iplayer_url_uses_bbc_iplayer_key()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var iplayer = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.Upsert(e, ServiceKeys.BbcIplayer, iplayer, null);
            EpisodeServicePresence.SetSpotifyIdentity(e, null);
            EpisodeServicePresence.SetYouTubeIdentity(e, null);
        });
        var json = ToLegacyJson(episode);

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch!.Services.Should().ContainKey(ServiceKeys.BbcIplayer);
        patch.Services.Should().NotContainKey(ServiceKeys.BbcSounds);
        patch.Services![ServiceKeys.BbcIplayer].Url.Should().Be(iplayer);
    }

    [Fact(DisplayName =
        "When stored JSON has urls.internetArchive and no services map, " +
        "the catalog patch adds services.internetArchive.")]
    public void patch_from_internet_archive_url_adds_archive_service()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var archive = new Uri($"https://archive.org/details/{_fixture.CreateSpotifyId()}");
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.Upsert(e, ServiceKeys.InternetArchive, archive, null);
            EpisodeServicePresence.SetSpotifyIdentity(e, null);
            EpisodeServicePresence.SetYouTubeIdentity(e, null);
        });
        var json = ToLegacyJson(episode);

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch!.Services.Should().ContainKey(ServiceKeys.InternetArchive);
        patch.Services![ServiceKeys.InternetArchive].Url.Should().Be(archive);
    }

    [Fact(DisplayName =
        "When stored JSON already has services.youtube but urls.spotify is uncovered, " +
        "the catalog patch keeps the YouTube service and adds Spotify, because hydrate merges into the existing map.")]
    public void patch_from_partial_services_keeps_existing_youtube_and_adds_spotify()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(
            podcast,
            _fixture.CreateSpotifyId(),
            _fixture.CreateYouTubeId());
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(stored);
        var spotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored);
        var youTubeUrl = EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.YouTube);
        var spotifyUrl = EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.Spotify);
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = stored.Id,
            ["podcastId"] = stored.PodcastId,
            ["youTubeId"] = youTubeId,
            ["spotifyId"] = spotifyId,
            ["urls"] = new Dictionary<string, string?>
            {
                ["youtube"] = youTubeUrl!.ToString(),
                ["spotify"] = spotifyUrl!.ToString()
            },
            ["services"] = new Dictionary<string, object>
            {
                [ServiceKeys.YouTube] = new Dictionary<string, string?>
                {
                    ["url"] = youTubeUrl.ToString()
                }
            }
        });

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch!.Services.Should().ContainKey(ServiceKeys.YouTube);
        patch.Services.Should().ContainKey(ServiceKeys.Spotify);
        patch.Services![ServiceKeys.YouTube].Url.Should().Be(youTubeUrl);
        patch.Services[ServiceKeys.Spotify].Url.Should().Be(spotifyUrl);
        patch.Ids!.YouTube.Should().Be(youTubeId);
        patch.Ids.Spotify.Should().Be(spotifyId);
    }

    [Fact(DisplayName =
        "When stored JSON already has services and nested ids covering every legacy url and top-level id, " +
        "no catalog patch is created, because the document is already dual-written.")]
    public void patch_is_not_created_for_complete_dual_write_document()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = episode.Id,
            ["podcastId"] = episode.PodcastId,
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
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeFalse();
        patch.Should().BeNull();
    }

    [Fact(DisplayName =
        "When stored JSON has no urls or platform ids, no catalog patch is created, " +
        "because empty episodes are not written.")]
    public void patch_is_not_created_for_empty_document()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = _fixture.CreateGuid(),
            ["podcastId"] = _fixture.CreateGuid(),
            ["title"] = _fixture.CreateTitle()
        });

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeFalse();
        patch.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a catalog patch is built from raw JSON, EpisodeId and PodcastId are the document id and podcastId, because the patch must target that item only.")]
    public void patch_identity_comes_from_the_json_document_being_processed()
    {
        // Arrange
        var firstPodcast = _fixture.CreatePodcast();
        var firstEpisode = _fixture.CreateStoredEpisodeWithSpotifyOnly(firstPodcast);
        var secondPodcast = _fixture.CreatePodcast();
        var secondEpisode = _fixture.CreateStoredEpisodeWithSpotifyOnly(secondPodcast);
        var firstJson = ToLegacyJson(firstEpisode);
        var secondJson = ToLegacyJson(secondEpisode);

        // Act
        var firstCreated = EpisodeServiceCatalogPatchFactory.TryCreate(firstJson, out var firstPatch);
        var secondCreated = EpisodeServiceCatalogPatchFactory.TryCreate(secondJson, out var secondPatch);

        // Assert
        firstCreated.Should().BeTrue();
        secondCreated.Should().BeTrue();
        firstPatch!.EpisodeId.Should().Be(firstEpisode.Id);
        firstPatch.PodcastId.Should().Be(firstPodcast.Id);
        secondPatch!.EpisodeId.Should().Be(secondEpisode.Id);
        secondPatch.PodcastId.Should().Be(secondPodcast.Id);
        EpisodeServiceCatalogPatchIdentity.Matches(firstJson, firstPatch, out _).Should().BeTrue();
        EpisodeServiceCatalogPatchIdentity.Matches(secondJson, secondPatch, out _).Should().BeTrue();
        EpisodeServiceCatalogPatchIdentity.Matches(firstJson, secondPatch, out var crossReason).Should().BeFalse();
        crossReason.Should().Be("id mismatch");
    }

    [Fact(DisplayName =
        "When apply is off, the backfill processor counts catalog-patch candidates and does not patch Cosmos.")]
    public async Task dry_run_counts_candidates_without_patching()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var json = ToLegacyJson(episode);
        var sut = _mocker.CreateInstance<EpisodeServiceBackfillProcessor>();

        // Act
        var report = await sut.RunAsync([json], apply: false);

        // Assert
        report.Candidates.Should().Be(1);
        report.Saved.Should().Be(0);
        report.Applied.Should().BeFalse();
        _repository.Verify(
            x => x.PatchServicesAndIds(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, EpisodeServiceLink>?>(),
                It.IsAny<EpisodeIds?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When apply is on, the processor patches services and ids from raw JSON even if a typed GetEpisode would already look hydrated, " +
        "because persist must not depend on in-memory hydrate.")]
    public async Task apply_patches_from_raw_json_without_loading_the_typed_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var json = ToLegacyJson(episode);
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
    }

    [Fact(DisplayName =
        "When apply patches from leftover JSON onto a catalog-empty stored episode, title, description, and lang stay as they were, " +
        "because the writer only sets services and nested ids.")]
    public async Task apply_does_not_rewrite_legacy_fields_on_the_stored_document()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        episode.Language = null;
        var title = episode.Title;
        var description = episode.Description;
        var spotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode);
        var json = ToLegacyJson(episode);
        episode.Services = null;
        episode.Ids = null;
        var repo = new InMemoryEpisodeRepository();
        repo.Seed(episode);
        var sut = new EpisodeServiceBackfillProcessor(
            new InMemoryBackfillEpisodeRepository(repo),
            new JsonEpisodeCatalogPatchSource(),
            NullLogger<EpisodeServiceBackfillProcessor>.Instance);

        // Act
        var report = await sut.RunAsync([json], apply: true);

        // Assert
        report.Saved.Should().Be(1);
        var stored = repo.GetStored(episode.Id);
        stored.Title.Should().Be(title);
        stored.Description.Should().Be(description);
        stored.Language.Should().BeNull();
        stored.Services.Should().ContainKey(ServiceKeys.Spotify);
        stored.Ids!.Spotify.Should().Be(spotifyId);
        repo.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the catalog patch target is missing from Cosmos, apply increments Missing and does not count a save.")]
    public async Task apply_counts_missing_when_patch_target_is_absent()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var json = ToLegacyJson(episode);
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
    }

    [Fact(DisplayName =
        "When stored JSON has only leftover Spotify url and top-level id, Classify returns null " +
        "because leftover is merged into catalog before the empty-services skip check.")]
    public void classify_leftover_only_document_is_a_candidate_not_both_null()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var json = ToLegacyJson(episode);

        // Act
        var skip = EpisodeServiceCatalogPatchFactory.Classify(json);

        // Assert
        skip.Should().BeNull();
        EpisodeServiceCatalogPatchFactory.TryCreate(json, out _).Should().BeTrue();
    }

    private static string ToLegacyJson(Episode episode, string? extraPropertyValue = null)
    {
        var urls = new Dictionary<string, string?>();
        var spotifyUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify);
        var appleUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple);
        var youTubeUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube);
        var bbcUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer)
                     ?? EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcSounds);
        var archiveUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.InternetArchive);
        if (spotifyUrl is not null)
        {
            urls["spotify"] = spotifyUrl.ToString();
        }

        if (appleUrl is not null)
        {
            urls["apple"] = appleUrl.ToString();
        }

        if (youTubeUrl is not null)
        {
            urls["youtube"] = youTubeUrl.ToString();
        }

        if (bbcUrl is not null)
        {
            urls["bbc"] = bbcUrl.ToString();
        }

        if (archiveUrl is not null)
        {
            urls["internetArchive"] = archiveUrl.ToString();
        }

        var payload = new Dictionary<string, object?>
        {
            ["id"] = episode.Id,
            ["podcastId"] = episode.PodcastId,
            ["title"] = episode.Title,
            ["description"] = episode.Description,
            ["lang"] = episode.Language,
            ["urls"] = urls,
            ["spotifyId"] = EpisodeServicePresence.SpotifyEpisodeId(episode),
            ["appleId"] = EpisodeServicePresence.AppleEpisodeId(episode),
            ["youTubeId"] = EpisodeServicePresence.YouTubeEpisodeId(episode)
        };
        var images = EpisodeServicePresence.ToEpisodeImages(episode);
        if (images is not null)
        {
            payload["images"] = new Dictionary<string, string?>
            {
                ["spotify"] = images.Spotify?.ToString(),
                ["apple"] = images.Apple?.ToString(),
                ["youtube"] = images.YouTube?.ToString(),
                ["other"] = images.Other?.ToString()
            };
        }

        if (extraPropertyValue is not null)
        {
            payload["curatorNote"] = extraPropertyValue;
        }

        return JsonSerializer.Serialize(payload);
    }
}
