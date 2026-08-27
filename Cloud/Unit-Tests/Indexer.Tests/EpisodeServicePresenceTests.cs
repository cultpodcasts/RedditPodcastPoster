// pragma: allowlist secret
using FluentAssertions; // pragma: allowlist secret
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using Xunit; // pragma: allowlist secret

namespace Indexer.Tests; // pragma: allowlist secret

public class EpisodeServicePresenceTests // pragma: allowlist secret
{
    [Fact(DisplayName =
        "Hydrate stores a service URL and image adjacent under the catalog JSON key when only legacy urls/images are present, so BBC artwork is no longer dumped in images.other while the URL lives in urls.bbc.")]
    public void Hydrate_places_bbc_iplayer_url_and_other_image_together()
    {
        // Arrange
        var episode = new Episode
        {
            Urls = new ServiceUrls
            {
                BBC = new Uri("https://www.bbc.co.uk/iplayer/episode/p0abcd12")
            },
            Images = new EpisodeImages
            {
                Other = new Uri("https://ichef.bbci.co.uk/images/ic/1200x675/p0artwork.jpg")
            }
        };

        // Act
        EpisodeServicePresence.Hydrate(episode); // pragma: allowlist secret

        // Assert
        episode.Services.Should().ContainKey(ServiceKeys.BbcIplayer);
        episode.Services![ServiceKeys.BbcIplayer].Url.Should()
            .Be(new Uri("https://www.bbc.co.uk/iplayer/episode/p0abcd12"));
        episode.Services[ServiceKeys.BbcIplayer].Image.Should()
            .Be(new Uri("https://ichef.bbci.co.uk/images/ic/1200x675/p0artwork.jpg"));
    }

    [Fact(DisplayName =
        "SyncLegacy dual-writes adjacent services back onto urls/images so existing Cosmos SQL indexers and admin forms that still read the split shape keep working during migration.")]
    public void Sync_legacy_copies_vimeo_image_into_images_other()
    {
        // Arrange
        var episode = new Episode
        {
            Services = new Dictionary<string, EpisodeServiceLink> // pragma: allowlist secret
            {
                [ServiceKeys.Vimeo] = new()
                {
                    Url = new Uri("https://vimeo.com/123456789"),
                    Image = new Uri("https://i.vimeocdn.com/video/123456789-d_640")
                }
            }
        };

        // Act
        EpisodeServicePresence.SyncLegacy(episode); // pragma: allowlist secret

        // Assert
        episode.Urls.BBC.Should().BeNull();
        episode.Images.Should().NotBeNull();
        episode.Images!.Other.Should().Be(new Uri("https://i.vimeocdn.com/video/123456789-d_640"));
    }

    [Fact(DisplayName =
        "Hydrate copies top-level Spotify/Apple/YouTube ids into the nested ids object so presence of a reconstructable service is the id, not a named URL slot.")]
    public void Hydrate_syncs_nested_ids_from_top_level_ids()
    {
        // Arrange
        var episode = new Episode
        {
            SpotifyId = "4rOoJ6Egrf8K2IrywzwOMk",
            AppleId = 9876543210,
            YouTubeId = "abc123DEF45"
        };

        // Act
        EpisodeServicePresence.Hydrate(episode); // pragma: allowlist secret

        // Assert
        episode.Ids.Should().NotBeNull();
        episode.Ids!.Spotify.Should().Be("4rOoJ6Egrf8K2IrywzwOMk");
        episode.Ids.Apple.Should().Be(9876543210);
        episode.Ids.YouTube.Should().Be("abc123DEF45");
    }

    [Fact(DisplayName =
        "SyncIds writes nested ids back onto the legacy top-level Spotify/Apple/YouTube id fields so matching and Cosmos SQL keep working during dual-write.")]
    public void Sync_ids_copies_nested_ids_onto_top_level_fields()
    {
        // Arrange
        var episode = new Episode
        {
            Ids = new EpisodeIds // pragma: allowlist secret
            {
                Spotify = "nestedSpotifyId",
                Apple = 111,
                YouTube = "nestedYouTubeId"
            }
        };

        // Act
        EpisodeServicePresence.SyncIds(episode); // pragma: allowlist secret

        // Assert
        episode.SpotifyId.Should().Be("nestedSpotifyId");
        episode.AppleId.Should().Be(111);
        episode.YouTubeId.Should().Be("nestedYouTubeId");
    }
}
