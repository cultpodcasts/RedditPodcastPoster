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
