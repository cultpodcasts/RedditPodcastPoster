// pragma: allowlist secret
using FluentAssertions; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using Xunit; // pragma: allowlist secret

namespace Indexer.Tests; // pragma: allowlist secret

public class ServiceCatalogTests
{
    [Theory(DisplayName =
        "Service catalog maps a URL host and path to the JSON key used for icons, so Vimeo/Netflix/BBC Sounds/iPlayer are identifiable without a hard-coded UI switch.")]
    [InlineData("https://vimeo.com/123456789", "vimeo")]
    [InlineData("https://www.netflix.com/title/80057281", "netflix")]
    [InlineData("https://www.bbc.co.uk/sounds/play/p0example", "bbcSounds")]
    [InlineData("https://www.bbc.co.uk/iplayer/episode/p0abcd12", "bbcIplayer")]
    [InlineData("https://archive.org/details/harbour-vale-ep", "internetArchive")]
    [InlineData("https://www.primevideo.com/detail/0EXAMPLE", "amazonPrime")]
    public void Resolves_well_known_hosts_to_stable_json_keys(string url, string expectedKey)
    {
        // Arrange
        var uri = new Uri(url);

        // Act
        var key = ServiceCatalog.TryResolveKey(uri);

        // Assert
        key.Should().Be(expectedKey);
        ServiceCatalog.TryGet(key!, out var descriptor).Should().BeTrue();
        descriptor.Icon.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName =
        "Service catalog slugs an unknown host into a letter-digit JSON key so a future documentary site can still carry an icon slot before it is added to the well-known list.")]
    public void Slugs_unknown_host_to_alnum_key()
    {
        // Arrange
        var uri = new Uri("https://www.dailymotion.com/video/xexample");

        // Act
        var key = ServiceCatalog.ResolveOrHostKey(uri);

        // Assert
        key.Should().Be("dailymotioncom");
    }
}
