using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Models.Services;

namespace Indexer.Tests;

public class EpisodeServicePresenceTests
{
    private readonly DomainTestFixture _fixture = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact(DisplayName =
        "Leftover Cosmos urls and top-level platform ids are ignored on deserialize and omitted on serialize, so a later full Save withers orphan leftover JSON.")]
    public void leftover_urls_and_top_level_ids_wither_on_round_trip()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var leftoverUrl = _fixture.DefaultSpotifyUrl(spotifyId);
        var catalogUrl = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var json =
            $$"""
            {
              "title": "Untitled",
              "spotifyId": "{{spotifyId}}",
              "youTubeId": "",
              "urls": { "spotify": "{{leftoverUrl}}" },
              "ids": { "spotify": "{{spotifyId}}" },
              "services": { "spotify": { "url": "{{catalogUrl}}" } }
            }
            """;

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(json, SerializerOptions)!;
        var written = JsonSerializer.Serialize(episode, SerializerOptions);

        // Assert
        EpisodeServicePresence.SpotifyEpisodeId(episode).Should().Be(spotifyId);
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify).Should().Be(catalogUrl);
        written.Should().NotContain("\"urls\"");
        written.Should().NotContain("\"spotifyId\"");
        written.Should().NotContain("\"youTubeId\"");
        written.Should().NotContain("\"appleId\"");
        written.Should().Contain("\"ids\"");
        written.Should().Contain("\"services\"");
    }

    [Fact(DisplayName =
        "TryGetUrl reads only the catalog services map, so leftover JSON that was ignored on deserialize cannot supply a listen URL.")]
    public void try_get_url_uses_catalog_only()
    {
        // Arrange
        var catalogUrl = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = catalogUrl }
            };
        });

        // Act
        var url = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify);

        // Assert
        url.Should().Be(catalogUrl);
    }

    [Fact(DisplayName =
        "ToEpisodeImages exposes Spotify/Apple/YouTube art only. Extra catalog art stays on services.{key}.image; search uses CoalescedImage.")]
    public void to_episode_images_does_not_collapse_vimeo_art_into_other()
    {
        // Arrange
        var vimeoUrl = new Uri("https://vimeo.com/123456789");
        var vimeoArt = new Uri("https://i.vimeocdn.com/video/123456789-d_640");
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, ServiceLink>
            {
                [StreamingServiceWire.ToKey(StreamingService.Vimeo)] = new() { Url = vimeoUrl, Image = vimeoArt }
            };
        });

        // Act
        var projected = EpisodeServicePresence.ToEpisodeImages(episode);

        // Assert
        projected.Should().BeNull();
        EpisodeServicePresence.TryGetImage(episode, StreamingServiceWire.ToKey(StreamingService.Vimeo)).Should().Be(vimeoArt);
        EpisodeServicePresence.CoalescedImage(episode).Should().Be(vimeoArt);
        EpisodeServicePresence.TryGetUrl(episode, StreamingServiceWire.ToKey(StreamingService.BbcIplayer)).Should().BeNull();
    }

    [Fact(DisplayName =
        "CoalescedImage prefers YouTube catalog art, then Spotify, then Apple, then remaining ImageCoalesceOrder keys including Vimeo.")]
    public void coalesced_image_follows_image_coalesce_order()
    {
        // Arrange
        var youTubeArt = new Uri("https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg");
        var spotifyArt = new Uri("https://i.scdn.co/image/spotifycoverart00");
        var appleArt = new Uri("https://is1-ssl.mzstatic.com/image/thumb/cover.jpg");
        var vimeoArt = new Uri("https://i.vimeocdn.com/video/987654321-d_640");
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, ServiceLink>
            {
                [StreamingServiceWire.ToKey(StreamingService.Vimeo)] = new() { Image = vimeoArt },
                [ServiceKeys.Apple] = new() { Image = appleArt },
                [ServiceKeys.Spotify] = new() { Image = spotifyArt },
                [ServiceKeys.YouTube] = new() { Image = youTubeArt }
            };
        });

        // Act
        var withYouTube = EpisodeServicePresence.CoalescedImage(episode);
        episode.Services!.Remove(ServiceKeys.YouTube);
        var withSpotify = EpisodeServicePresence.CoalescedImage(episode);
        episode.Services.Remove(ServiceKeys.Spotify);
        var withApple = EpisodeServicePresence.CoalescedImage(episode);
        episode.Services.Remove(ServiceKeys.Apple);
        var withVimeo = EpisodeServicePresence.CoalescedImage(episode);

        // Assert
        withYouTube.Should().Be(youTubeArt);
        withSpotify.Should().Be(spotifyArt);
        withApple.Should().Be(appleArt);
        withVimeo.Should().Be(vimeoArt);
    }

    [Fact(DisplayName =
        "SetSpotifyIdentity writes only nested ids, because leftover top-level spotifyId is retired.")]
    public void set_spotify_identity_writes_nested_ids_only()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var episode = _fixture.CreateEpisode();

        // Act
        EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyId);

        // Assert
        episode.Ids.Should().NotBeNull();
        episode.Ids!.Spotify.Should().Be(spotifyId);
        EpisodeServicePresence.SpotifyEpisodeId(episode).Should().Be(spotifyId);
    }

    [Fact(DisplayName =
        "NormalizeCatalog drops the retired other catalog key and does not invent a listen slot, because leftover images JSON is ignored and cover art comes from remaining catalog services.")]
    public void normalize_catalog_drops_retired_other_catalog_key()
    {
        // Arrange
        var leftoverArt = new Uri("https://cdn.example.test/cover.jpg");
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, ServiceLink>
            {
                ["other"] = new() { Image = leftoverArt }
            };
        });

        // Act
        EpisodeServicePresence.NormalizeCatalog(episode);

        // Assert
        episode.Services.Should().BeNull();
        EpisodeServicePresence.ToEpisodeImages(episode).Should().BeNull();
        EpisodeServicePresence.CoalescedImage(episode).Should().BeNull();
    }

    [Fact(DisplayName =
        "Leftover Cosmos images JSON is ignored on deserialize and omitted on serialize, so cover art must come from services.*.image.")]
    public void leftover_images_wither_on_round_trip()
    {
        // Arrange
        var leftoverArt = new Uri("https://cdn.example.test/leftover-cover.jpg");
        var catalogArt = new Uri("https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg");
        var json =
            $$"""
            {
              "title": "Untitled",
              "images": { "youtube": "{{leftoverArt}}", "other": "{{leftoverArt}}" },
              "services": { "youtube": { "image": "{{catalogArt}}" } }
            }
            """;

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(json, SerializerOptions)!;
        var written = JsonSerializer.Serialize(episode, SerializerOptions);

        // Assert
        EpisodeServicePresence.CoalescedImage(episode).Should().Be(catalogArt);
        written.Should().NotContain("\"images\"");
        written.Should().Contain("\"services\"");
    }

    [Fact(DisplayName =
        "YouTubeEpisodeId falls back to the video id in services.youtube.url when nested ids.youtube is absent " +
        "(catalogue playable projections after Move to TV / film).")]
    public void youtube_episode_id_from_services_url_when_ids_absent()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Ids = null;
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            };
        });

        // Act
        var id = EpisodeServicePresence.YouTubeEpisodeId(episode);

        // Assert
        id.Should().Be(videoId);
    }

    [Fact(DisplayName =
        "Leftover Cosmos top-level youTubeId on an Episode is ignored on deserialize and omitted on serialize, " +
        "because Episode has no youTubeId member — nested ids.youtube and services.youtube.url only.")]
    public void leftover_top_level_youtube_id_withers_and_is_not_canonical()
    {
        // Arrange
        var leftoverId = _fixture.CreateYouTubeId();
        var nestedId = _fixture.CreateYouTubeId();
        var urlId = _fixture.CreateYouTubeId();
        var catalogUrl = _fixture.DefaultYouTubeUrl(urlId);
        var json =
            $$"""
            {
              "title": "Untitled",
              "youTubeId": "{{leftoverId}}",
              "ids": { "youtube": "{{nestedId}}" },
              "services": { "youtube": { "url": "{{catalogUrl}}" } }
            }
            """;

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(json, SerializerOptions)!;
        var fromPresence = EpisodeServicePresence.YouTubeEpisodeId(episode);
        var written = JsonSerializer.Serialize(episode, SerializerOptions);

        // Assert
        fromPresence.Should().Be(nestedId);
        written.Should().NotContain("\"youTubeId\"");
        written.Should().Contain("\"ids\"");
    }

    [Fact(DisplayName =
        "SyncCatalogueYouTubeId on a TvShowEpisode sets YouTubeId from services.youtube.url " +
        "and clears it when the YouTube service url is removed.")]
    public void sync_catalogue_youtube_id_sets_and_clears_on_tv_show_episode()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = new RedditPodcastPoster.Models.TvShows.TvShowEpisode(_fixture.CreateTitle())
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            }
        };

        // Act
        EpisodeServicePresence.SyncCatalogueYouTubeId(episode);

        // Assert
        episode.YouTubeId.Should().Be(videoId);

        // Arrange
        episode.Services = null;

        // Act
        EpisodeServicePresence.SyncCatalogueYouTubeId(episode);

        // Assert
        episode.YouTubeId.Should().BeNull();
    }

    [Fact(DisplayName =
        "SyncCatalogueYouTubeId on a Film sets YouTubeId from services.youtube.url " +
        "so film search push can project youtubeId without nested ids.")]
    public void sync_catalogue_youtube_id_sets_on_film()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var film = new RedditPodcastPoster.Models.Films.Film(_fixture.CreateTitle())
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            }
        };

        // Act
        EpisodeServicePresence.SyncCatalogueYouTubeId(film);

        // Assert
        film.YouTubeId.Should().Be(videoId);
    }

    [Fact(DisplayName =
        "SyncCatalogueYouTubeId on a NewsReport sets YouTubeId from services.youtube.url " +
        "and clears it when the YouTube service url is removed.")]
    public void sync_catalogue_youtube_id_sets_and_clears_on_news_report()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var report = new RedditPodcastPoster.Models.News.NewsReport(_fixture.CreateTitle())
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            }
        };

        // Act
        EpisodeServicePresence.SyncCatalogueYouTubeId(report);

        // Assert
        report.YouTubeId.Should().Be(videoId);

        // Arrange
        report.Services = null;

        // Act
        EpisodeServicePresence.SyncCatalogueYouTubeId(report);

        // Assert
        report.YouTubeId.Should().BeNull();
    }

    [Fact(DisplayName =
        "Film, TvShowEpisode, and NewsReport implement ICatalogueYouTubeItem while podcast Episode does not, " +
        "so SyncCatalogueYouTubeId cannot bind leftover top-level youTubeId onto episodes.")]
    public void catalogue_youtube_item_is_only_film_tv_and_news()
    {
        // Arrange
        var film = new RedditPodcastPoster.Models.Films.Film(_fixture.CreateTitle());
        var tv = new RedditPodcastPoster.Models.TvShows.TvShowEpisode(_fixture.CreateTitle());
        var news = new RedditPodcastPoster.Models.News.NewsReport(_fixture.CreateTitle());
        var episode = _fixture.CreateEpisode();

        // Act / Assert
        film.Should().BeAssignableTo<RedditPodcastPoster.Models.Catalogue.ICatalogueYouTubeItem>();
        tv.Should().BeAssignableTo<RedditPodcastPoster.Models.Catalogue.ICatalogueYouTubeItem>();
        news.Should().BeAssignableTo<RedditPodcastPoster.Models.Catalogue.ICatalogueYouTubeItem>();
        episode.Should().NotBeAssignableTo<RedditPodcastPoster.Models.Catalogue.ICatalogueYouTubeItem>();
    }

    [Fact(DisplayName =
        "KeysWithIdButNoUrl lists exactly the platforms that have a nested id and no services url, so only those are materialised on Move.")]
    public void keys_with_id_but_no_url_lists_only_platforms_missing_a_url()
    {
        // Arrange
        var episode = _fixture.CreateStoredEpisode(_fixture.CreatePodcast());
        episode.Services = null;
        episode.Ids = new EpisodeIds
        {
            Spotify = _fixture.CreateSpotifyId(),
            Apple = _fixture.CreateAppleId(),
            YouTube = _fixture.CreateYouTubeId()
        };
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, _fixture.Create<Uri>(), null);

        // Act
        var keys = EpisodeServicePresence.KeysWithIdButNoUrl(episode);

        // Assert
        keys.Should().BeEquivalentTo([ServiceKeys.Spotify, ServiceKeys.YouTube]);
    }

    [Fact(DisplayName =
        "EnsureServiceUrlsFromIds fills a missing url from the id and keeps an existing image and any existing url, so an image-only slot becomes playable without losing its cover art.")]
    public void ensure_service_urls_from_ids_keeps_images_and_existing_urls()
    {
        // Arrange
        var episode = _fixture.CreateStoredEpisode(_fixture.CreatePodcast());
        episode.Services = null;
        var spotifyId = _fixture.CreateSpotifyId();
        var youTubeId = _fixture.CreateYouTubeId();
        episode.Ids = new EpisodeIds { Spotify = spotifyId, YouTube = youTubeId };
        var spotifyImage = _fixture.Create<Uri>();
        var youTubeUrl = _fixture.Create<Uri>();
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, spotifyImage);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, youTubeUrl, null);

        // Act
        var filled = EpisodeServicePresence.EnsureServiceUrlsFromIds(episode);

        // Assert
        filled.Should().BeTrue();
        episode.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(spotifyId));
        episode.Services[ServiceKeys.Spotify].Image.Should().Be(spotifyImage);
        episode.Services[ServiceKeys.YouTube].Url.Should().Be(youTubeUrl);
    }

    [Fact(DisplayName =
        "Upsert of a YouTube url fills a missing ids.youtube with the url's video id, because search reads only the nested id.")]
    public void upsert_youtube_url_fills_missing_nested_id()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e => e.Ids = null);

        // Act
        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(videoId), null);

        // Assert
        episode.Ids!.YouTube.Should().Be(videoId);
    }

    [Fact(DisplayName =
        "Upsert of a YouTube url keeps an ids.youtube that is already set, because only a missing id is filled.")]
    public void upsert_youtube_url_keeps_existing_nested_id()
    {
        // Arrange
        var storedId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e => EpisodeServicePresence.SetYouTubeIdentity(e, storedId));

        // Act
        EpisodeServicePresence.Upsert(
            episode, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId()), null);

        // Assert
        episode.Ids!.YouTube.Should().Be(storedId);
    }

    [Fact(DisplayName =
        "Upsert of a YouTube url with no video id (a playlist) leaves ids.youtube empty.")]
    public void upsert_youtube_playlist_url_leaves_nested_id_empty()
    {
        // Arrange
        var episode = _fixture.CreateEpisode(e => e.Ids = null);
        var playlist = new Uri("https://www.youtube.com/playlist?list=PLsynthetic0000000000");

        // Act
        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, playlist, null);

        // Assert
        EpisodeServicePresence.NestedYouTubeId(episode).Should().BeNull();
    }

    [Fact(DisplayName =
        "NestedYouTubeId is empty for an episode with services.youtube.url but no ids.youtube, while YouTubeEpisodeId falls back to the url.")]
    public void nested_youtube_id_has_no_url_fallback()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Ids = null;
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            };
        });

        // Act
        var nested = EpisodeServicePresence.NestedYouTubeId(episode);
        var withFallback = EpisodeServicePresence.YouTubeEpisodeId(episode);

        // Assert
        nested.Should().BeNull();
        withFallback.Should().Be(videoId);
    }

    [Theory(DisplayName =
        "FillYouTubeIdFromUrl writes ids.youtube from watch, youtu.be, shorts and live urls when it is missing.")]
    [InlineData("https://www.youtube.com/watch?v={0}")]
    [InlineData("https://youtu.be/{0}")]
    [InlineData("https://www.youtube.com/shorts/{0}")]
    [InlineData("https://www.youtube.com/live/{0}")]
    public void fill_youtube_id_from_url_forms(string format)
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, null);
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri(string.Format(format, videoId)) }
            };
        });

        // Act
        var filled = EpisodeServicePresence.FillYouTubeIdFromUrl(episode);

        // Assert
        filled.Should().BeTrue();
        episode.Ids!.YouTube.Should().Be(videoId);
    }

    [Fact(DisplayName =
        "FillYouTubeIdFromUrl does nothing when ids.youtube is already set, so it is safe to call on every save.")]
    public void fill_youtube_id_from_url_is_idempotent()
    {
        // Arrange
        var storedId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, storedId);
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId()) }
            };
        });

        // Act
        var filled = EpisodeServicePresence.FillYouTubeIdFromUrl(episode);

        // Assert
        filled.Should().BeFalse();
        episode.Ids!.YouTube.Should().Be(storedId);
    }

    [Fact(DisplayName =
        "Clearing YouTube (id set to null, then the service removed) leaves no ids.youtube, because the url is not re-read before it is removed.")]
    public void clearing_youtube_does_not_refill_the_id()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, videoId);
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(videoId), null);
        });

        // Act
        EpisodeServicePresence.SetYouTubeIdentity(episode, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, null, null);

        // Assert
        EpisodeServicePresence.NestedYouTubeId(episode).Should().BeNull();
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube).Should().BeNull();
    }
}
