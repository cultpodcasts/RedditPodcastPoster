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
}
