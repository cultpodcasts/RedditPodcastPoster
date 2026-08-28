// pragma: allowlist secret
using FluentAssertions; // pragma: allowlist secret
using RedditPodcastPoster.Episodes.TestSupport.Fixtures; // pragma: allowlist secret
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using Xunit; // pragma: allowlist secret

namespace Indexer.Tests; // pragma: allowlist secret

public class EpisodeServicePresenceTests // pragma: allowlist secret
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Hydrate stores a service URL and image adjacent under the catalog JSON key when only legacy urls/images are present, so BBC artwork is no longer dumped in images.other while the URL lives in urls.bbc.")]
    public void Hydrate_places_bbc_iplayer_url_and_other_image_together()
    {
        // Arrange
        var iplayerUrl = new Uri("https://www.bbc.co.uk/iplayer/episode/p0abcd12");
        var leftoverArt = new Uri("https://ichef.bbci.co.uk/images/ic/1200x675/p0artwork.jpg");
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Urls = new ServiceUrls { BBC = iplayerUrl };
            e.Images = new EpisodeImages { Other = leftoverArt };
        });

        // Act
        EpisodeServicePresence.Hydrate(episode); // pragma: allowlist secret

        // Assert
        episode.Services.Should().ContainKey(ServiceKeys.BbcIplayer);
        episode.Services![ServiceKeys.BbcIplayer].Url.Should().Be(iplayerUrl);
        episode.Services[ServiceKeys.BbcIplayer].Image.Should().Be(leftoverArt);
        episode.Services.Should().NotContainKey("other");
    }

    [Fact(DisplayName =
        "SyncLegacy dual-writes adjacent services back onto urls/images so existing Cosmos SQL indexers and admin forms that still read the split shape keep working during migration.")]
    public void Sync_legacy_copies_vimeo_image_into_images_other()
    {
        // Arrange
        var vimeoUrl = new Uri("https://vimeo.com/123456789");
        var vimeoArt = new Uri("https://i.vimeocdn.com/video/123456789-d_640");
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, EpisodeServiceLink> // pragma: allowlist secret
            {
                [ServiceKeys.Vimeo] = new() { Url = vimeoUrl, Image = vimeoArt }
            };
        });

        // Act
        EpisodeServicePresence.SyncLegacy(episode); // pragma: allowlist secret

        // Assert
        episode.Urls.BBC.Should().BeNull();
        episode.Images.Should().NotBeNull();
        episode.Images!.Other.Should().Be(vimeoArt);
    }

    [Fact(DisplayName =
        "Hydrate copies top-level Spotify/Apple/YouTube ids into the nested ids object so presence of a reconstructable service is the id, not a named URL slot.")]
    public void Hydrate_syncs_nested_ids_from_top_level_ids()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.SpotifyId = spotifyId;
            e.AppleId = appleId;
            e.YouTubeId = youTubeId;
        });

        // Act
        EpisodeServicePresence.Hydrate(episode); // pragma: allowlist secret

        // Assert
        episode.Ids.Should().NotBeNull();
        episode.Ids!.Spotify.Should().Be(spotifyId);
        episode.Ids.Apple.Should().Be(appleId);
        episode.Ids.YouTube.Should().Be(youTubeId);
    }

    [Fact(DisplayName =
        "SyncIds writes nested ids back onto the legacy top-level Spotify/Apple/YouTube id fields so matching and Cosmos SQL keep working during dual-write.")]
    public void Sync_ids_copies_nested_ids_onto_top_level_fields()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.SpotifyId = string.Empty;
            e.YouTubeId = string.Empty;
            e.Ids = new EpisodeIds // pragma: allowlist secret
            {
                Spotify = spotifyId,
                Apple = appleId,
                YouTube = youTubeId
            };
        });

        // Act
        EpisodeServicePresence.SyncIds(episode); // pragma: allowlist secret

        // Assert
        episode.SpotifyId.Should().Be(spotifyId);
        episode.AppleId.Should().Be(appleId);
        episode.YouTubeId.Should().Be(youTubeId);
    }

    [Fact(DisplayName =
        "Hydrate does not invent an other listen service from leftover images.other, because services are an explicit catalog and leftover other is cover art.")]
    public void Hydrate_keeps_unmatched_images_other_off_the_services_map()
    {
        // Arrange
        var leftoverArt = new Uri("https://cdn.example.test/cover.jpg");
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, EpisodeServiceLink> // pragma: allowlist secret
            {
                ["other"] = new() { Image = leftoverArt }
            };
            e.Images = new EpisodeImages { Other = leftoverArt };
        });

        // Act
        EpisodeServicePresence.Hydrate(episode); // pragma: allowlist secret

        // Assert
        episode.Services.Should().BeNull();
        episode.Images!.Other.Should().Be(leftoverArt);
        EpisodeServicePresence.CoalescedImage(episode).Should().Be(leftoverArt);
    }
}
