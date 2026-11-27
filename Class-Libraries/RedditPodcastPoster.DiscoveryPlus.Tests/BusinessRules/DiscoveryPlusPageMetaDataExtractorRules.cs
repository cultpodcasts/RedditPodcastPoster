using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.DiscoveryPlus.Extensions;
using RedditPodcastPoster.DiscoveryPlus.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.DiscoveryPlus.Tests.BusinessRules;

public class DiscoveryPlusPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public DiscoveryPlusPageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "discovery+ page extract GETs the catalogue URL and reads Open Graph fields, " +
        "so submit can ingest a discovery+ watch/title page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("discovery+");
        meta.ShowName.Should().Be(title);
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "discovery+ catalogue hub extract sets ShowName from og:title when it equals the series brand and JSON-LD is absent, " +
        "so GET submit lookup still returns podcastName for series hubs.")]
    public async Task extracts_hub_title_as_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{seriesName}\" /></head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seriesName);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("discovery+");
    }

    [Fact(DisplayName =
        "discovery+ series extract populates ShowName from JSON-LD TVSeries, " +
        "so GET submit lookup can return podcastName for a series catalogue page.")]
    public async Task extracts_tvseries_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("discovery+");
    }

    [Fact(DisplayName =
        "discovery+ film pages leave ShowName null even when a TVSeries-looking brand is present, " +
        "because a film has no parent series for podcastName attach.")]
    public async Task movie_pages_do_not_set_show_name()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{filmTitle}\" />" +
            $"<meta property=\"og:type\" content=\"video.movie\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmTitle}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "A discovery+ film with a Watch-style marketing description and a w=500 beam cover stores the cleaned " +
        "synopsis and upgrades the image to w=2000, matching Max film submit cleaning.")]
    public async Task movie_page_cleans_watch_description_and_upgrades_beam_cover()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var episodeLabel = _fixture.CreateTitle();
        var blurb = $"{_fixture.CreateTitle(4)} {_fixture.CreateTitle(3)}.";
        var filmId = _fixture.CreateGuid();
        var coverAssetId = _fixture.CreateGuid();
        var url = new Uri($"https://www.discoveryplus.com/movie/{filmId}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{filmTitle}\" />" +
            $"<meta property=\"og:description\" content=\"Watch {filmTitle}: Season  1, Episode 1 - {episodeLabel} . {blurb}\" />" +
            $"<meta property=\"og:image\" content=\"https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/{filmId}/{coverAssetId}?w=500\" />" +
            "<meta property=\"og:type\" content=\"video.movie\" />" +
            "<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmTitle}\"}}" +
            "</script></head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.MadeAsFilm.Should().BeTrue();
        meta.ShowName.Should().BeNull();
        meta.Description.Should().Be(blurb);
        meta.Description.Should().NotStartWith("Watch ");
        meta.Image.Should().NotBeNull();
        meta.Image!.AbsoluteUri.Should().Contain("w=2000");
        meta.Image.AbsoluteUri.Should().Contain(coverAssetId.ToString());
    }

    [Fact(DisplayName =
        "A discovery+ episode page uses og:image:alt as ShowName and the description episode title as Title, " +
        "so siblings share one series instead of each OG title becoming its own TvShow (#1587).")]
    public async Task episode_page_uses_image_alt_show_name_and_description_episode_title()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var url = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-slug/{episodeId}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"Watch {showName} | Season  1 Episode 1 | discovery+\" />" +
            $"<meta property=\"og:image:alt\" content=\"{showName}\" />" +
            $"<meta property=\"og:description\" content=\"Watch {showName}: Season  1, Episode 1 - {episodeTitle} . More blurb.\" />" +
            "<meta property=\"og:type\" content=\"video.tv_show\" />" +
            "</head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().Be(showName);
        meta.Title.Should().Be(episodeTitle);
        meta.Description.Should().Be("More blurb.");
        meta.Publisher.Should().Be("discovery+");
        meta.MadeAsFilm.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A discovery+ episode page prefers a beam still under the episode UUID at w=2000 over the series og:image cover, " +
        "so submit stores the episode landscape still instead of a 500-wide series poster.")]
    public async Task episode_page_prefers_episode_uuid_still_over_series_cover()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var seriesAssetId = _fixture.CreateGuid();
        var stillAssetId = _fixture.CreateGuid();
        var url = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-slug/{episodeId}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"Watch {showName} | Season  1 Episode 1 | discovery+\" />" +
            $"<meta property=\"og:image:alt\" content=\"{showName}\" />" +
            $"<meta property=\"og:description\" content=\"Watch {showName}: Season  1, Episode 1 - {episodeTitle} . Synopsis blurb.\" />" +
            $"<meta property=\"og:image\" content=\"https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/{showId}/{seriesAssetId}?w=500\" />" +
            "<meta property=\"og:type\" content=\"video.tv_show\" />" +
            "</head><body>" +
            $"<img src=\"https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/{episodeId}/{stillAssetId}?w=2000\" />" +
            "</body></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.Description.Should().Be("Synopsis blurb.");
        meta.Image.Should().NotBeNull();
        meta.Image!.AbsoluteUri.Should().Contain(episodeId.ToString());
        meta.Image.AbsoluteUri.Should().Contain(stillAssetId.ToString());
        meta.Image.AbsoluteUri.Should().Contain("w=2000");
        meta.Image.AbsoluteUri.Should().NotContain(seriesAssetId.ToString());
    }

    [Fact(DisplayName =
        "A discovery+ episode page without og:image:alt still takes ShowName from the Watch {show} | Season… OG title, " +
        "so missing alt does not leave ShowName as the full Watch|Season string (#1587).")]
    public async Task episode_page_without_image_alt_parses_show_name_from_watch_season_title()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var url = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-slug/{episodeId}");
        var ogTitle = $"Watch {showName} | Season  1 Episode 1 | discovery+";
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{ogTitle}\" />" +
            "<meta property=\"og:type\" content=\"video.tv_show\" />" +
            "</head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().Be(showName);
        meta.Title.Should().NotBeNullOrWhiteSpace();
        meta.Title.Should().NotContain("Watch ", because: "episode title must not stay the full Watch|Season OG string");
        meta.Title.Should().NotBe(showName);
        meta.Publisher.Should().Be("discovery+");
    }

    [Fact(DisplayName =
        "A discovery+ description episode title that contains a period is kept whole until the space-dot blurb separator, " +
        "so mid-title abbreviations are not truncated.")]
    public async Task description_episode_title_keeps_periods_until_space_dot_blurb()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var episodeTitle = $"{_fixture.CreateTitle(2)}. {_fixture.CreateTitle(2)}";
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var url = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-slug/{episodeId}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"Watch {showName} | Season  1 Episode 1 | discovery+\" />" +
            $"<meta property=\"og:image:alt\" content=\"{showName}\" />" +
            $"<meta property=\"og:description\" content=\"Watch {showName}: Season  1, Episode 1 - {episodeTitle} . More blurb.\" />" +
            "<meta property=\"og:type\" content=\"video.tv_show\" />" +
            "</head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        episodeTitle.Should().Contain(".");
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(showName);
    }

    [Fact(DisplayName =
        "A discovery+ show hub strips the Watch prefix from og:title for ShowName, " +
        "so the series brand is not stored as 'Watch …'.")]
    public async Task hub_strips_watch_prefix_for_show_name()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateGuid()}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"Watch {showName} | discovery+\" />" +
            $"<meta property=\"og:image:alt\" content=\"{showName}\" />" +
            "<meta property=\"og:type\" content=\"video.tv_show\" />" +
            "</head></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().Be(showName);
        meta.Title.Should().Be(showName);
    }

    [Fact(DisplayName =
        "A discovery+ episode page prefers the earliest geo availability window start over a year-only " +
        "og:video:release_date, so SSR shells that omit JSON-LD air dates still get a calendar Release.")]
    public async Task episode_page_uses_availability_window_over_year_only_og_release()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var url = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-slug/{episodeId}");
        var yearOnly = DomainTestFixture.UtcDateDaysAgo(40).Year;
        var usStart = DomainTestFixture.UtcAtTime(-3, TimeSpan.FromHours(2));
        var deStart = DomainTestFixture.UtcAtTime(-3, TimeSpan.FromHours(23));
        var expectedRelease = DateTime.SpecifyKind(usStart.Date, DateTimeKind.Utc);
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"Watch {showName} | Season  1 Episode 1 | discovery+\" />" +
            $"<meta property=\"og:image:alt\" content=\"{showName}\" />" +
            $"<meta property=\"og:description\" content=\"Watch {showName}: Season  1, Episode 1 - {episodeTitle} . More blurb.\" />" +
            "<meta property=\"og:type\" content=\"video.tv_show\" />" +
            $"<meta property=\"og:video:release_date\" content=\"{yearOnly}\" />" +
            "</head><body>" +
            $"<script>{{\"pageLocales\":{{\"windows\":{{\"us/en\":[\"{usStart:yyyy-MM-dd'T'HH:mm:ss'Z'}\",\"2099-12-31T23:59:00Z\"],\"de/de\":[\"{deStart:yyyy-MM-dd'T'HH:mm:ss'Z'}\",\"2036-10-31T22:59:00Z\"]}}}}}}</script>" +
            "</body></html>");
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(showName);
        meta.Release.Should().Be(expectedRelease);
        meta.Duration.Should().BeNull();
    }

    [Fact(DisplayName =
        "discovery+ page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<DiscoveryPlusPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddDiscoveryPlusServices registers a catalog-keyed adapter for discovery+ URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_discoveryplus_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDiscoveryPlusServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.ResolveService(new Uri("https://example.com/")).Should().Be(StreamingService.DiscoveryPlus);
    }

    private static HttpResponseMessage OkHtml(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
