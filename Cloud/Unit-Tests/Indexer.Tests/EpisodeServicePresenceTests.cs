using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

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
            e.Services = new Dictionary<string, EpisodeServiceLink>
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
        "ToEpisodeImages projects non-default catalog artwork as Other, because leftover images members are retired and admin GET still needs that shape.")]
    public void to_episode_images_projects_vimeo_catalog_art_as_other()
    {
        // Arrange
        var vimeoUrl = new Uri("https://vimeo.com/123456789");
        var vimeoArt = new Uri("https://i.vimeocdn.com/video/123456789-d_640");
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.Vimeo] = new() { Url = vimeoUrl, Image = vimeoArt }
            };
        });

        // Act
        var projected = EpisodeServicePresence.ToEpisodeImages(episode);

        // Assert
        projected.Should().NotBeNull();
        projected!.Other.Should().Be(vimeoArt);
        projected.YouTube.Should().BeNull();
        projected.Spotify.Should().BeNull();
        projected.Apple.Should().BeNull();
        EpisodeServicePresence.CoalescedImage(episode).Should().Be(vimeoArt);
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer).Should().BeNull();
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
            e.Services = new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.Vimeo] = new() { Image = vimeoArt },
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
            e.Services = new Dictionary<string, EpisodeServiceLink>
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
}
