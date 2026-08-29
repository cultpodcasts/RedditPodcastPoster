using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Episodes;
using Xunit;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class EpisodeServiceCatalogPatchRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker;
    private readonly Mock<IEpisodeRepository> _repository;

    public EpisodeServiceCatalogPatchRules()
    {
        _mocker = new AutoMocker();
        _repository = _mocker.GetMock<IEpisodeRepository>();
        _mocker.Use(NullLogger<EpisodeServiceBackfillProcessor>.Instance);
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
        patch.Services![ServiceKeys.Spotify].Url.Should().Be(episode.Urls.Spotify);
        patch.Ids!.Spotify.Should().Be(episode.SpotifyId);
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
        var art = new Uri($"https://i.ytimg.com/vi/{episode.YouTubeId}/maxresdefault.jpg");
        episode.Images = new EpisodeImages { YouTube = art };
        var json = ToLegacyJson(episode);

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch!.Services.Should().ContainKey(ServiceKeys.YouTube);
        patch.Services![ServiceKeys.YouTube].Url.Should().Be(episode.Urls.YouTube);
        patch.Services[ServiceKeys.YouTube].Image.Should().Be(art);
        patch.Ids!.YouTube.Should().Be(episode.YouTubeId);
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
            e.AppleId = appleId;
            e.Urls.Apple = _fixture.DefaultAppleUrl(appleId);
            EpisodeServicePresence.SetSpotifyIdentity(e, null);
            EpisodeServicePresence.SetYouTubeIdentity(e, null);
        });
        var json = ToLegacyJson(episode);

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch!.Services.Should().ContainKey(ServiceKeys.Apple);
        patch.Services![ServiceKeys.Apple].Url.Should().Be(episode.Urls.Apple);
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
            e.Urls.BBC = iplayer;
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
            e.Urls.InternetArchive = archive;
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
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = stored.Id,
            ["podcastId"] = stored.PodcastId,
            ["youTubeId"] = stored.YouTubeId,
            ["spotifyId"] = stored.SpotifyId,
            ["urls"] = new Dictionary<string, string?>
            {
                ["youtube"] = stored.Urls.YouTube!.ToString(),
                ["spotify"] = stored.Urls.Spotify!.ToString()
            },
            ["services"] = new Dictionary<string, object>
            {
                [ServiceKeys.YouTube] = new Dictionary<string, string?>
                {
                    ["url"] = stored.Urls.YouTube.ToString()
                }
            }
        });

        // Act
        var created = EpisodeServiceCatalogPatchFactory.TryCreate(json, out var patch);

        // Assert
        created.Should().BeTrue();
        patch!.Services.Should().ContainKey(ServiceKeys.YouTube);
        patch.Services.Should().ContainKey(ServiceKeys.Spotify);
        patch.Services![ServiceKeys.YouTube].Url.Should().Be(stored.Urls.YouTube);
        patch.Services[ServiceKeys.Spotify].Url.Should().Be(stored.Urls.Spotify);
        patch.Ids!.YouTube.Should().Be(stored.YouTubeId);
        patch.Ids.Spotify.Should().Be(stored.SpotifyId);
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
            ["spotifyId"] = episode.SpotifyId,
            ["ids"] = new Dictionary<string, string?> { ["spotify"] = episode.SpotifyId },
            ["urls"] = new Dictionary<string, string?> { ["spotify"] = episode.Urls.Spotify!.ToString() },
            ["services"] = new Dictionary<string, object>
            {
                [ServiceKeys.Spotify] = new Dictionary<string, string?>
                {
                    ["url"] = episode.Urls.Spotify.ToString()
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
        _repository.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
        _repository.Verify(x => x.GetEpisode(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
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
        _repository.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
        _repository.Verify(x => x.GetEpisode(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
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
        var spotifyUrl = episode.Urls.Spotify;
        var spotifyId = episode.SpotifyId;
        var json = ToLegacyJson(episode);
        episode.Services = null;
        episode.Ids = null;
        var repo = new InMemoryEpisodeRepository();
        repo.Seed(episode);
        var sut = new EpisodeServiceBackfillProcessor(repo, NullLogger<EpisodeServiceBackfillProcessor>.Instance);

        // Act
        var report = await sut.RunAsync([json], apply: true);

        // Assert
        report.Saved.Should().Be(1);
        var stored = repo.GetStored(episode.Id);
        stored.Title.Should().Be(title);
        stored.Description.Should().Be(description);
        stored.Language.Should().BeNull();
        stored.Urls.Spotify.Should().Be(spotifyUrl);
        stored.SpotifyId.Should().Be(spotifyId);
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
        _repository.Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    private static string ToLegacyJson(Episode episode, string? extraPropertyValue = null)
    {
        var urls = new Dictionary<string, string?>();
        if (episode.Urls.Spotify is not null)
        {
            urls["spotify"] = episode.Urls.Spotify.ToString();
        }

        if (episode.Urls.Apple is not null)
        {
            urls["apple"] = episode.Urls.Apple.ToString();
        }

        if (episode.Urls.YouTube is not null)
        {
            urls["youtube"] = episode.Urls.YouTube.ToString();
        }

        if (episode.Urls.BBC is not null)
        {
            urls["bbc"] = episode.Urls.BBC.ToString();
        }

        if (episode.Urls.InternetArchive is not null)
        {
            urls["internetArchive"] = episode.Urls.InternetArchive.ToString();
        }

        var payload = new Dictionary<string, object?>
        {
            ["id"] = episode.Id,
            ["podcastId"] = episode.PodcastId,
            ["title"] = episode.Title,
            ["description"] = episode.Description,
            ["lang"] = episode.Language,
            ["urls"] = urls,
            ["spotifyId"] = episode.SpotifyId,
            ["appleId"] = episode.AppleId,
            ["youTubeId"] = episode.YouTubeId
        };
        if (episode.Images is not null)
        {
            payload["images"] = new Dictionary<string, string?>
            {
                ["spotify"] = episode.Images.Spotify?.ToString(),
                ["apple"] = episode.Images.Apple?.ToString(),
                ["youtube"] = episode.Images.YouTube?.ToString(),
                ["other"] = episode.Images.Other?.ToString()
            };
        }

        if (extraPropertyValue is not null)
        {
            payload["curatorNote"] = extraPropertyValue;
        }

        return JsonSerializer.Serialize(payload);
    }
}
