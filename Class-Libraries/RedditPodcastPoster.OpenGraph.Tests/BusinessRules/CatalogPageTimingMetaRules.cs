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
        "CatalogPageTimingMeta recovers Duration and Release from generic SEO Runtime / Release Date labels " +
        "when JSON-LD omits timing, so SSR catalogues without Open Graph still fill prepare meta.")]
    public void seo_runtime_and_release_date_year_labels()
    {
        // Arrange — mirrors movie-details SSR labels (not OG / not JSON-LD).
        const string html =
            "<html><body>Documentary 2022 1h 28m" +
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
        "so SEO pages that only spell out hours and mins still get length.")]
    public void seo_prose_runtime()
    {
        // Arrange
        const string html =
            "Sex, Lies and the College Cult has a running time of 1 hour and 28 mins.";

        // Act
        var duration = CatalogPageTimingMeta.TryDurationFromHtml(html);

        // Assert
        duration.Should().Be(new TimeSpan(1, 28, 0));
    }

    [Fact(DisplayName =
        "CatalogPageTimingMeta recovers Duration from Peacock JSON-LD TVEpisode PT56M " +
        "but does not coerce Release from nested trailer VideoObject uploadDate.")]
    public void peacock_json_ld_duration_ignores_trailer_upload_date()
    {
        // Arrange — mirrors watch-online episode json-ld-wrapper (trailer-only date).
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
        release.Should().BeNull();
    }

    [Fact(DisplayName =
        "CatalogPageTimingMeta recovers Release from Peacock JSON-LD TVEpisode datePublished " +
        "when present, preferring the catalogue node over nested trailer uploadDate.")]
    public void peacock_json_ld_episode_date_published_wins_over_trailer()
    {
        // Arrange
        const string html =
            """
            <script type="application/ld+json">
            {"@context":"http://schema.org","@graph":[{
              "@type":"TVEpisode","name":"Episode 1","duration":"PT56M",
              "datePublished":"2020-07-14T00:00:00.000Z",
              "video":[{"@type":"VideoObject","name":"Trailer","duration":"PT116S","uploadDate":"2024-10-10T16:40:38.166Z"}]
            }]}
            </script>
            """;

        // Act
        var (duration, release) = CatalogPageTimingMeta.Coalesce(null, null, html);

        // Assert
        duration.Should().Be(TimeSpan.FromMinutes(56));
        release.Should().Be(new DateTime(2020, 7, 14, 0, 0, 0, DateTimeKind.Utc));
    }
}
