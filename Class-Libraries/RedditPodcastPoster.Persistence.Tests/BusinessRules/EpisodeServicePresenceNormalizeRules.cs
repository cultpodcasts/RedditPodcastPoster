using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class EpisodeServicePresenceNormalizeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "NormalizeCatalog renames legacy unknown-host key iviewabcnetau to abcIview, " +
        "so Compact and pull SQL project the first-class catalog key into svc.")]
    public void normalize_renames_legacy_iview_slug_to_abc_iview()
    {
        // Arrange
        var url = new Uri($"https://iview.abc.net.au/video/{_fixture.CreateYouTubeId()}");
        var episode = new Episode
        {
            Services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal)
            {
                ["iviewabcnetau"] = new ServiceLink { Url = url }
            }
        };

        // Act
        EpisodeServicePresence.NormalizeCatalog(episode);

        // Assert
        episode.Services.Should().NotBeNull();
        episode.Services!.Should().NotContainKey("iviewabcnetau");
        episode.Services.Should().ContainKey(StreamingServiceWire.ToKey(StreamingService.AbcIview));
        episode.Services[StreamingServiceWire.ToKey(StreamingService.AbcIview)].Url.Should().Be(url);
    }

    [Fact(DisplayName =
        "NormalizeCatalog keeps an existing abcIview link when both abcIview and iviewabcnetau are present, " +
        "dropping the legacy slug without clobbering the catalog URL.")]
    public void normalize_prefers_catalog_key_when_both_present()
    {
        // Arrange
        var catalogUrl = new Uri($"https://iview.abc.net.au/video/{_fixture.CreateYouTubeId()}");
        var legacyUrl = new Uri($"https://iview.abc.net.au/video/{_fixture.CreateYouTubeId()}");
        var catalogKey = StreamingServiceWire.ToKey(StreamingService.AbcIview);
        var episode = new Episode
        {
            Services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal)
            {
                [catalogKey] = new ServiceLink { Url = catalogUrl },
                ["iviewabcnetau"] = new ServiceLink { Url = legacyUrl }
            }
        };

        // Act
        EpisodeServicePresence.NormalizeCatalog(episode);

        // Assert
        episode.Services.Should().NotBeNull();
        episode.Services!.Should().NotContainKey("iviewabcnetau");
        episode.Services[catalogKey].Url.Should().Be(catalogUrl);
    }
}
