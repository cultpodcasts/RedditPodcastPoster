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

    [Fact(DisplayName =
        "CatalogPageTimingMeta recovers Duration and Release from Peacock SEO Runtime / Release Date labels, " +
        "so US watch-online HTML that omits Open Graph timing still fills prepare meta.")]
    public void peacock_seo_runtime_and_release_date_year()
    {
        // Arrange — mirrors watch-online movie details SSR (not OG).
        const string html =
            "<html><body>Documentary 2022 1h 28m NBC Peacock" +
            "<dt>Release Date</dt><dd>2022</dd>" +
            "<dt>Runtime</dt><dd>1h 28m</dd>" +
            "</body></html>";

        // Act
        var (duration, release) = CatalogPageTimingMeta.Coalesce(null, null, html);

        // Assert
        duration.Should().Be(new TimeSpan(1, 28, 0));
        release.Should().Be(new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact(DisplayName =
        "CatalogPageTimingMeta recovers Duration from FAQ prose running time, " +
        "so Peacock SEO pages that only spell out hours and mins still get length.")]
    public void peacock_seo_prose_runtime()
    {
        // Arrange
        const string html =
            "Sex, Lies and the College Cult has a running time of 1 hour and 28 mins. Stream it on Peacock.";

        // Act
        var duration = CatalogPageTimingMeta.TryDurationFromHtml(html);

        // Assert
        duration.Should().Be(new TimeSpan(1, 28, 0));
    }

    [Fact(DisplayName =
        "CatalogPageTimingMeta recovers Duration and Release from Peacock JSON-LD " +
        "(TVEpisode PT56M + trailer uploadDate) when the episode UI hides both.")]
    public void peacock_json_ld_duration_and_upload_date()
    {
        // Arrange — mirrors watch-online episode json-ld-wrapper.
        const string html =
            """
            <div class="json-ld-wrapper" data-testid="jsonLd">
            <script type="application/ld+json">
            {"@context":"http://schema.org","@graph":[{
              "@type":"TVEpisode","name":"Episode 1","duration":"PT56M",
              "video":[{"@type":"VideoObject","name":"Trailer","duration":"PT116S","uploadDate":"2024-10-10T16:40:38.166Z"}]
            }]}
            </script>
            </div>
            """;

        // Act
        var (duration, release) = CatalogPageTimingMeta.Coalesce(null, null, html);

        // Assert
        duration.Should().Be(TimeSpan.FromMinutes(56));
        release.Should().Be(new DateTime(2024, 10, 10, 16, 40, 38, 166, DateTimeKind.Utc));
    }
}
