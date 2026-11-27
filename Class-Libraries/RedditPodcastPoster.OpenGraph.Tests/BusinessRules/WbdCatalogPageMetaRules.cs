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

    [Fact(DisplayName =
        "WbdCatalogPageMeta does not treat a show UUID that starts with e+digit as an episode path segment, " +
        "so /show/{e1…} hubs stay hubs instead of false episode matches.")]
    public void show_uuid_starting_with_e_digit_is_not_episode_path()
    {
        // Arrange — Guid whose D-string starts with e1… (would false-match the old ^e\d+ prefix)
        var showId = GuidStartingWithE1Digit();
        showId.ToString("D").Should().MatchRegex("^e[0-9]");
        var hub = new Uri($"https://www.discoveryplus.com/show/{showId}");
        var episode = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-rise-of-top-g/{_fixture.CreateGuid()}");

        // Act
        var hubIsEpisode = WbdCatalogPageMeta.IsEpisodePath(hub);
        var episodeIsEpisode = WbdCatalogPageMeta.IsEpisodePath(episode);

        // Assert
        hubIsEpisode.Should().BeFalse();
        episodeIsEpisode.Should().BeTrue();
    }

    [Fact(DisplayName =
        "WbdCatalogPageMeta.TryEpisodeSynopsis reads the rich-text paragraph after Season N, Episode M, " +
        "strips a leading colon, and ignores the following pricing CTA.")]
    public void try_episode_synopsis_reads_season_episode_paragraph()
    {
        // Arrange
        var synopsis = $"{_fixture.CreateTitle(4)} {_fixture.CreateTitle(5)}.";
        var html =
            "<html><body>" +
            "<h1><strong>Season  1, Episode 2</strong></h1>" +
            $"<p class=\"text-left\">: {synopsis}</p>" +
            "<p class=\"text-left\">Plans start at $6.99/month.</p>" +
            "</body></html>";

        // Act
        var got = WbdCatalogPageMeta.TryEpisodeSynopsis(html);

        // Assert
        got.Should().Be(synopsis);
    }

    [Fact(DisplayName =
        "WbdCatalogPageMeta.TryEpisodeSynopsis ignores Season N, Episode M text in og:description and an early cookie " +
        "<p>, and returns the synopsis after the SSR h1 heading instead.")]
    public void try_episode_synopsis_ignores_og_season_text_and_early_cookie_paragraph()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var seriesBlurb = $"{_fixture.CreateTitle(4)} {_fixture.CreateTitle(3)}.";
        var cookieCopy = "Accept all cookies";
        var synopsis = $"{_fixture.CreateTitle(4)} {_fixture.CreateTitle(5)}.";
        var html =
            "<html><head>" +
            $"<meta property=\"og:description\" content=\"Watch {showName}: Season 1, Episode 2 - {episodeTitle} . {seriesBlurb}\" />" +
            "</head><body>" +
            $"<p class=\"cookie\">{cookieCopy}</p>" +
            "<h1><strong>Season 1, Episode 2</strong></h1>" +
            $"<p class=\"text-left\">: {synopsis}</p>" +
            "</body></html>";

        // Act
        var got = WbdCatalogPageMeta.TryEpisodeSynopsis(html);

        // Assert
        got.Should().Be(synopsis);
        got.Should().NotBe(cookieCopy);
        got.Should().NotContain(seriesBlurb);
    }

    [Fact(DisplayName =
        "WbdCatalogPageMeta.ResolveEpisodeDescription on an episode path returns empty when SSR synopsis is missing, " +
        "so the cleaned series og:description blurb is never stored as the episode description.")]
    public void resolve_episode_description_empty_when_ssr_synopsis_missing()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var url = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-slug/{episodeId}");
        var seriesBlurb = $"{_fixture.CreateTitle(5)} {_fixture.CreateTitle(4)}.";
        var og =
            $"Watch {_fixture.CreateTitle()}: Season  1, Episode 1 - {_fixture.CreateTitle()} . {seriesBlurb}";

        // Act
        var description = WbdCatalogPageMeta.ResolveEpisodeDescription(url, "<html></html>", og);

        // Assert
        description.Should().BeEmpty();
        description.Should().NotBe(seriesBlurb);
    }

    /// <summary>Little-endian first group yields a D-string starting with <c>e1</c>.</summary>
    private static Guid GuidStartingWithE1Digit() =>
        new([0x33, 0x30, 0xd0, 0xe1, 0x8c, 0xad, 0x9a, 0x4b, 0x93, 0x05, 0x20, 0x42, 0xfe, 0xdc, 0x75, 0x0c]);
}
