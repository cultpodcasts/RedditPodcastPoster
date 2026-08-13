using FluentAssertions;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace Indexer.Tests;

public class ShortnerShareImageMetadataTests
{
    [Fact(DisplayName =
        "New shortener metadata stores search-index YouTube image token as wide for og/twitter cards.")]
    public void Stores_youtube_search_token_as_wide()
    {
        // Arrange
        const string youTubeId = "griffinsong42";
        var episode = new Episode
        {
            YouTubeId = youTubeId,
            Images = new EpisodeImages
            {
                YouTube = new Uri($"https://i.ytimg.com/vi/{youTubeId}/maxresdefault.jpg")
            }
        };
        var metadata = new MetaData
        {
            EpisodeTitle = "Sample title",
            ReleaseDate = new DateOnly(2026, 7, 29),
            Duration = TimeSpan.FromMinutes(30)
        };

        // Act
        ShortnerShareImageMetadata.Apply(metadata, episode);

        // Assert
        metadata.Image.Should().Be("yx");
        metadata.YoutubeId.Should().Be(youTubeId);
        metadata.ImageAspect.Should().Be(ShareImageAspect.Wide);
    }

    [Fact(DisplayName =
        "New shortener metadata stores Spotify search-index token as square when there is no YouTube/iPlayer/Archive.")]
    public void Stores_spotify_token_as_square()
    {
        // Arrange
        var episode = new Episode
        {
            Images = new EpisodeImages
            {
                Spotify = new Uri("https://i.scdn.co/image/ab6765ferngully00cover")
            }
        };
        var metadata = new MetaData
        {
            EpisodeTitle = "Sample title",
            ReleaseDate = new DateOnly(2026, 7, 29),
            Duration = TimeSpan.FromMinutes(30)
        };

        // Act
        ShortnerShareImageMetadata.Apply(metadata, episode);

        // Assert
        metadata.Image.Should().Be("sab6765ferngully00cover");
        metadata.YoutubeId.Should().BeNull();
        metadata.ImageAspect.Should().Be(ShareImageAspect.Square);
    }

    [Fact(DisplayName =
        "BBC Sounds keeps square aspect; BBC iPlayer is wide even with square cover art.")]
    public void Bbc_sounds_square_iplayer_wide()
    {
        // Arrange
        var cover = new Uri("https://i.scdn.co/image/ab6765cover");
        var sounds = new Episode
        {
            Images = new EpisodeImages { Spotify = cover },
            Urls = new ServiceUrls { BBC = new Uri("https://www.bbc.co.uk/sounds/play/p0example") }
        };
        var iplayer = new Episode
        {
            Images = new EpisodeImages { Spotify = cover },
            Urls = new ServiceUrls { BBC = new Uri("https://www.bbc.co.uk/iplayer/episode/p0example") }
        };

        // Act
        var soundsAspect = ShortnerShareImageMetadata.ResolveAspect(sounds, "sab6765cover", null);
        var iplayerAspect = ShortnerShareImageMetadata.ResolveAspect(iplayer, "sab6765cover", null);

        // Assert
        soundsAspect.Should().Be(ShareImageAspect.Square);
        iplayerAspect.Should().Be(ShareImageAspect.Wide);
    }

    [Fact(DisplayName =
        "Apply leaves image fields unset when the episode has no cover art.")]
    public void Leaves_image_fields_null_when_no_art()
    {
        // Arrange
        var episode = new Episode();
        var metadata = new MetaData
        {
            EpisodeTitle = "Sample title",
            ReleaseDate = new DateOnly(2026, 7, 29),
            Duration = TimeSpan.FromMinutes(30)
        };

        // Act
        ShortnerShareImageMetadata.Apply(metadata, episode);

        // Assert
        metadata.Image.Should().BeNull();
        metadata.YoutubeId.Should().BeNull();
        metadata.ImageAspect.Should().BeNull();
    }

    [Fact(DisplayName =
        "ShareImageAspect serializes to lowercase wide/square for Api-compatible shortener KV metadata.")]
    public void Share_image_aspect_serializes_as_lowercase_wire_values()
    {
        // Arrange
        var metadata = new MetaData
        {
            EpisodeTitle = "Sample title",
            ReleaseDate = new DateOnly(2026, 7, 29),
            Duration = TimeSpan.FromMinutes(30),
            ImageAspect = ShareImageAspect.Wide
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(metadata);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<MetaData>(
            """{"episodeTitle":"t","releaseDate":"2026-07-29","duration":"00:30:00","imageAspect":"square"}""");

        // Assert
        json.Should().Contain("\"imageAspect\":\"wide\"");
        roundTrip!.ImageAspect.Should().Be(ShareImageAspect.Square);
    }
}
