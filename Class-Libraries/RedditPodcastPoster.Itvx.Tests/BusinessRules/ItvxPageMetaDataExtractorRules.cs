using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Itvx.Extensions;
using RedditPodcastPoster.Itvx.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.Itvx.Tests.BusinessRules;

public class ItvxPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public ItvxPageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "ITVX GetMetaDataFromHtml extracts Open Graph from fixture HTML without a network fetch, " +
        "so Worker Browser Rendering can pass trusted HTML into Azure extract.")]
    public async Task get_meta_data_from_html_uses_fixture_html_without_network()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var html =
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>";
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaDataFromHtml(url, html);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("ITVX");
        meta.ShowName.Should().Be(title);
        _handler.LastRequestUri.Should().BeNull();
    }

    [Fact(DisplayName =
        "ITVX extract prefers __NEXT_DATA__ episode art, ISO duration, and broadcast release over Open Graph, " +
        "because watch pages expose the ITVX brand logo as og:image and often omit length/date in JSON-LD.")]
    public async Task next_data_wins_over_open_graph_for_image_duration_release()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var seriesName = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var duration = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(16);
        var release = DomainTestFixture.UtcAtTime(-30, new TimeSpan(21, 15, 0));
        var imageId = _fixture.CreateYouTubeId();
        var catalogueImage =
            $"https://ovp.itv.com/v2/images/special/{imageId}/itv_hub/01_Hero_DesktopCTV/16x9?distributionPartner=itv_hub&fallback=standard&w=2236&q=80&blur=0&bg=false";
        var brandLogo =
            "https://app.10ft.itv.com/itvstatic/assets/images/brands/itvx/itvx-logo-for-light-backgrounds.jpg?q=80&w=1366";
        var slug = _fixture.CreateYouTubeId();
        var programmeId = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.itv.com/watch/{slug}/{programmeId}/{_fixture.CreateYouTubeId()}");
        var nextData = System.Text.Json.JsonSerializer.Serialize(new
        {
            props = new
            {
                pageProps = new
                {
                    programme = new
                    {
                        title = seriesName,
                        imagePresets = new Dictionary<string, Dictionary<string, string>>
                        {
                            ["1920"] = new() { ["2x"] = catalogueImage }
                        }
                    },
                    episode = new
                    {
                        episodeTitle,
                        longDescription = description,
                        notFormattedDuration = "PT1H16M",
                        broadcastDateTime = release.ToString("o")
                    }
                }
            }
        });
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<meta property=\"og:image\" content=\"{brandLogo}\" />" +
            $"<script id=\"__NEXT_DATA__\" type=\"application/json\">{nextData}</script>" +
            $"</head></html>";
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaDataFromHtml(url, html);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Description.Should().Be(description);
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
        meta.Image.Should().Be(new Uri(catalogueImage));
    }

    [Fact(DisplayName =
        "ITVX extract resolves catalogue image templates from __NEXT_DATA__ when imagePresets are absent, " +
        "so prepare/submit still stores episode art instead of the brand logo.")]
    public async Task next_data_image_template_is_resolved_when_presets_missing()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var imageId = _fixture.CreateYouTubeId();
        var template =
            $"https://ovp.itv.com/v2/images/special/{imageId}/itv_hub/01_Hero_DesktopCTV/16x9?distributionPartner=itv_hub&fallback=standard&w={{width}}&h={{height}}&q={{quality}}&blur={{blur}}&bg={{bg}}";
        var expected =
            $"https://ovp.itv.com/v2/images/special/{imageId}/itv_hub/01_Hero_DesktopCTV/16x9?distributionPartner=itv_hub&fallback=standard&w=1920&h=1080&q=80&blur=0&bg=false";
        var url = new Uri(
            $"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var nextData = System.Text.Json.JsonSerializer.Serialize(new
        {
            props = new
            {
                pageProps = new
                {
                    episode = new
                    {
                        episodeTitle,
                        image = template
                    }
                }
            }
        });
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<script id=\"__NEXT_DATA__\" type=\"application/json\">{nextData}</script>" +
            $"</head></html>";
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaDataFromHtml(url, html);

        // Assert
        meta.Image.Should().Be(new Uri(expected));
    }

    [Fact(DisplayName =
        "ITVX brand watch extract sets ShowName from og:title when no TVSeries name exists, " +
        "because /watch/{brand}/{id} is the ITVX catalogue shape for series podcastName.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("ITVX");
        meta.ShowName.Should().Be(title);
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "ITVX episode watch URLs leave ShowName null when no TVSeries blob exists, " +
        "because og:title is the episode title and must not poison podcastName.")]
    public async Task episode_watch_path_does_not_set_show_name_from_title()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri(
            $"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "ITVX episode watch extract sets ShowName from JSON-LD TVSeries when present, " +
        "so podcastName attach still works without title fallback.")]
    public async Task episode_watch_path_sets_show_name_from_tvseries()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var url = new Uri(
            $"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
    }

    [Fact(DisplayName =
        "ITVX soft-wall homepage title without og:title fails extract, " +
        "because the shell is not catalogue metadata.")]
    public async Task homepage_shell_title_without_og_fails()
    {
        // Arrange
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml("<html><head><title>ITVX Homepage</title></head></html>");
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "ITVX brand watch extract sets ShowName from JSON-LD TVSeries even when that name equals og:title, " +
        "so GET submit lookup still returns podcastName when Open Graph skips title-equal series candidates.")]
    public async Task extracts_hub_title_as_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{seriesName}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seriesName);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("ITVX");
    }

    [Fact(DisplayName =
        "ITVX series extract populates ShowName from JSON-LD TVSeries, " +
        "so GET submit lookup can return podcastName for a series catalogue page.")]
    public async Task extracts_tvseries_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("ITVX");
    }

    [Fact(DisplayName =
        "ITVX film pages leave ShowName null even when a TVSeries-looking brand is present, " +
        "because a film has no parent series for podcastName attach.")]
    public async Task movie_pages_do_not_set_show_name()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{filmTitle}\" />" +
            $"<meta property=\"og:type\" content=\"video.movie\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmTitle}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "ITVX watch film without og:type still classifies as movie via primary @type=Movie, " +
        "so ShowName stays null and title fallback does not invent a podcastName.")]
    public async Task watch_path_movie_json_ld_without_og_type_is_movie()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{filmTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmTitle}\"}}" +
            $"</script></head></html>";
        _handler.Response = OkHtml(html);
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "ITVX series watch keeps ShowName from TVSeries even when a recommended Movie blob appears earlier, " +
        "because series evidence wins over carousel film JSON-LD.")]
    public async Task series_watch_prefers_tvseries_over_earlier_carousel_movie()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{seriesName}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmTitle}\"}}" +
            $"</script>" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>";
        _handler.Response = OkHtml(html);
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().Be(seriesName);
    }

    [Fact(DisplayName =
        "ITVX page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<ItvxPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddItvxServices registers a catalog-keyed adapter for ITVX URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_itvx_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddItvxServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.Itvx);
    }

    [Fact(DisplayName =
        "AddItvxServices configures Accept-Language and Sec-Fetch headers on the named HttpClient, " +
        "because ITVX forcibly closes bare scrapes that omit them.")]
    public void add_itvx_services_sets_browser_fetch_headers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddItvxServices();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        // Act
        var client = factory.CreateClient(nameof(ItvxPageMetaDataExtractor));

        // Assert
        client.DefaultRequestHeaders.AcceptLanguage.ToString().Should().Contain("en-GB");
        client.DefaultRequestHeaders.Accept.ToString().Should().Contain("text/html");
        client.DefaultRequestHeaders.GetValues("Sec-Fetch-Dest").Single().Should().Be("document");
        client.DefaultRequestHeaders.GetValues("Sec-Fetch-Mode").Single().Should().Be("navigate");
        client.DefaultRequestHeaders.GetValues("Sec-Fetch-Site").Single().Should().Be("none");
        client.DefaultRequestHeaders.GetValues("Sec-Fetch-User").Single().Should().Be("?1");
        client.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact(DisplayName =
        "AddItvxServices enables AutomaticDecompression on the ITVX SocketsHttpHandler, " +
        "because IHttpClientFactory defaults omit Accept-Encoding and ITVX hangs/resets those scrapes.")]
    public void add_itvx_services_enables_automatic_decompression()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddItvxServices();
        using var provider = services.BuildServiceProvider();
        var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        // Act
        var handler = handlerFactory.CreateHandler(nameof(ItvxPageMetaDataExtractor));
        var sockets = UnwrapToSocketsHttpHandler(handler);

        // Assert
        sockets.Should().NotBeNull(
            "AddItvxServices must ConfigurePrimaryHttpMessageHandler(CreateItvxSocketsHandler)");
        sockets!.AutomaticDecompression.Should().Be(DecompressionMethods.All);
    }

    private static SocketsHttpHandler? UnwrapToSocketsHttpHandler(HttpMessageHandler handler)
    {
        for (HttpMessageHandler? current = handler; current != null;)
        {
            if (current is SocketsHttpHandler sockets)
            {
                return sockets;
            }

            if (current is DelegatingHandler delegating)
            {
                current = delegating.InnerHandler;
                continue;
            }

            break;
        }

        return null;
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
