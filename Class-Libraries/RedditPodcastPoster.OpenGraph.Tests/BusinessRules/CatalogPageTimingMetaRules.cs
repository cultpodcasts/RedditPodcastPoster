using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.OpenGraph.Extractors;

namespace RedditPodcastPoster.OpenGraph.Tests.BusinessRules;

public class CatalogPageTimingMetaRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "CatalogPageTimingMeta recovers Release from Arte-style rights.begin when OG omits release, " +
        "so catalogue pages that only embed player rights freight still get an air date.")]
    public void rights_begin_recovers_release()
    {
        // Arrange
        var release = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var begin = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var html = $"{{\"rights\":{{\"begin\":\"{begin}\",\"end\":\"2099-01-01T00:00:00Z\"}}}}";

        // Act
        var recovered = CatalogPageTimingMeta.TryReleaseFromHtml(html);

        // Assert
        recovered.Should().Be(release);
    }

    [Fact(DisplayName =
        "CatalogPageTimingMeta ignores a bare JSON begin token outside a rights object, " +
        "so unrelated SPA payloads cannot coerce Release when OG omits it.")]
    public void bare_begin_outside_rights_does_not_win()
    {
        // Arrange
        var decoy = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var decoyIso = decoy.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var html = $"{{\"schedule\":{{\"begin\":\"{decoyIso}\"}},\"window\":{{\"begin\":\"{decoyIso}\"}}}}";

        // Act
        var recovered = CatalogPageTimingMeta.TryReleaseFromHtml(html);

        // Assert
        recovered.Should().BeNull();
    }
}
