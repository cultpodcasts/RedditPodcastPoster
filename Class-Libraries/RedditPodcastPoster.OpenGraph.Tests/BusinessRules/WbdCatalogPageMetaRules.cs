using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.OpenGraph.Extractors;

namespace RedditPodcastPoster.OpenGraph.Tests.BusinessRules;

public class WbdCatalogPageMetaRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "WbdCatalogPageMeta strips the Watch Season/Episode marketing lead-in from a discovery+ description, " +
        "so submit stores the synopsis blurb instead of the Watch prefix.")]
    public void clean_description_strips_discovery_plus_watch_lead_in()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var blurb = $"{_fixture.CreateTitle(4)} {_fixture.CreateTitle(3)}.";
        var raw =
            $"Watch {showName}: Season  1, Episode 2 - {episodeTitle} . {blurb}";

        // Act
        var cleaned = WbdCatalogPageMeta.CleanWatchMarketingDescription(raw);

        // Assert
        cleaned.Should().Be(blurb);
        cleaned.Should().NotStartWith("Watch ");
    }

    [Fact(DisplayName =
        "WbdCatalogPageMeta strips Max's on HBO Max lead-in from a Watch description, " +
        "so the stored synopsis does not keep the streaming marketing sentence.")]
    public void clean_description_strips_hbo_max_watch_lead_in()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var blurb = $"{_fixture.CreateTitle(4)} {_fixture.CreateTitle(3)}.";
        var raw =
            $"Watch {showName}: Season  1, Episode 2 - {episodeTitle} on HBO Max. {blurb}";

        // Act
        var cleaned = WbdCatalogPageMeta.CleanWatchMarketingDescription(raw);

        // Assert
        cleaned.Should().Be(blurb);
        cleaned.Should().NotContain("on HBO Max");
    }

    [Fact(DisplayName =
        "WbdCatalogPageMeta prefers a beam still under the episode UUID over the series og:image cover, " +
        "and rewrites w= to 2000 so submit does not keep a 500-wide series poster.")]
    public void try_episode_image_prefers_episode_uuid_still_at_w_2000()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var seriesAssetId = _fixture.CreateGuid();
        var stillAssetId = _fixture.CreateGuid();
        var url = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-slug/{episodeId}");
        var seriesCover =
            new Uri(
                $"https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/{showId}/{seriesAssetId}?w=500");
        var html =
            $"<html><body>" +
            $"<img src=\"https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/{showId}/{seriesAssetId}?w=500\" />" +
            $"<img src=\"https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/{episodeId}/{stillAssetId}?w=2000\" />" +
            $"</body></html>";

        // Act
        var image = WbdCatalogPageMeta.TryEpisodeImage(url, html, seriesCover);

        // Assert
        image.Should().NotBeNull();
        image!.AbsoluteUri.Should().Contain(episodeId.ToString());
        image.AbsoluteUri.Should().Contain(stillAssetId.ToString());
        image.AbsoluteUri.Should().Contain("w=2000");
        image.AbsoluteUri.Should().NotContain(seriesAssetId.ToString());
    }

    [Fact(DisplayName =
        "WbdCatalogPageMeta upgrades a beam og:image fallback from w=500 to w=2000 when no episode still is in the HTML, " +
        "so submit never leaves the half-width series cover size.")]
    public void try_episode_image_upgrades_og_fallback_width()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var coverAssetId = _fixture.CreateGuid();
        var url = new Uri(
            $"https://www.max.com/show/{showId}/s1/e1-slug/{episodeId}");
        var seriesCover =
            new Uri(
                $"https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/{showId}/{coverAssetId}?w=500");

        // Act
        var image = WbdCatalogPageMeta.TryEpisodeImage(url, "<html></html>", seriesCover);

        // Assert
        image.Should().NotBeNull();
        image!.AbsoluteUri.Should().Contain("w=2000");
        image.AbsoluteUri.Should().Contain(coverAssetId.ToString());
    }

    [Fact(DisplayName =
        "WbdCatalogPageMeta leaves a non-deliverable Warner CDN asset's w= unchanged, " +
        "so PreferFullBeamWidth only rewrites beam-images / wbd-images deliverable paths.")]
    public void prefer_full_beam_width_ignores_non_deliverable_warner_cdn()
    {
        // Arrange
        var assetId = _fixture.CreateGuid();
        var nonDeliverable = new Uri(
            $"https://static.warnermediacdn.com/assets/{assetId}?w=500");

        // Act
        var image = WbdCatalogPageMeta.PreferFullBeamWidth(nonDeliverable);

        // Assert
        image.Should().Be(nonDeliverable);
        image.AbsoluteUri.Should().Contain("w=500");
        image.AbsoluteUri.Should().NotContain("w=2000");
    }
}
