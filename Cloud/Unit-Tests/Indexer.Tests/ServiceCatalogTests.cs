using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace Indexer.Tests;

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
    [InlineData("https://www.paramountplus.com/shows/example/", "paramountPlus")]
    [InlineData("https://www.max.com/shows/example", "hboMax")]
    [InlineData("https://www.hbomax.com/series/urn:hbo:series:example", "hboMax")]
    [InlineData("https://www.playsuisse.ch/watch/example", "playSuisse")]
    [InlineData("https://www.tvnz.co.nz/shows/example", "tvnzPlus")]
    [InlineData("https://www.itv.com/watch/example/1a2345", "itvx")]
    [InlineData("https://www.channel4.com/programmes/example", "channel4")]
    [InlineData("https://www.all4.com/programmes/example", "channel4")]
    [InlineData("https://fawesome.tv/movies/1/example", "fawesome")]
    [InlineData("https://www.disneyplus.com/series/example", "disneyPlus")]
    [InlineData("https://www.discoveryplus.com/show/example", "discoveryPlus")]
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
        ServiceCatalog.TryGet("other", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Service catalog is an explicit list of defined destinations and does not include an other catch-all, because leftover images.other is cover art rather than a listen service.")]
    public void Catalog_does_not_include_other()
    {
        // Arrange
        var hostWithNoAlnum = new Uri("http://-/");

        // Act
        var keys = ServiceCatalog.All.Select(d => d.Key).ToArray();

        // Assert
        keys.Should().NotContain("other");
        ServiceCatalog.ImageCoalesceOrder.Should().NotContain("other");
        ServiceCatalog.SearchEncodedKeys.Should().NotContain("other");
        ServiceCatalog.KeyFromUnknownHost(hostWithNoAlnum).Should().BeNull();
    }
}
