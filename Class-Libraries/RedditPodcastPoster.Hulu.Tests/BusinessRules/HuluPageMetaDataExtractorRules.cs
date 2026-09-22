using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Hulu.Extensions;
using RedditPodcastPoster.Hulu.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.Hulu.Tests.BusinessRules;

public class HuluPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public HuluPageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Hulu page extract GETs the catalogue URL and reads Open Graph fields, " +
        "so submit can ingest a Hulu page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.hulu.com/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<HuluPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Hulu");
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "Hulu page extract fills Duration and Release from JSON-LD VideoObject ISO duration and datePublished, " +
        "so submit can store episode length and air date from the catalogue page.")]
    public async Task extracts_duration_and_release_from_json_ld()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-5, _fixture.CreateNonMidnightTimeOfDay());
        var iso = $"PT{duration.Hours}H{duration.Minutes}M{duration.Seconds}S";
        var published = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var url = new Uri($"https://www.hulu.com/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{title}\" />" +
            "<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"VideoObject\",\"duration\":\"{iso}\",\"datePublished\":\"{published}\"}}" +
            "</script></head></html>");
        var sut = _mocker.CreateInstance<HuluPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
    }

        [Fact(DisplayName =
        "Hulu page extract recovers Duration and Release from embedded duration.seconds and datePublished " +
        "when Open Graph omits them, so prepare stores timing the same way as a JSON-LD catalogue page.")]
    public async Task recovers_duration_and_release_from_embedded_html_when_og_omits_them()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var seconds = (int)duration.TotalSeconds;
        var published = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var url = new Uri($"https://www.hulu.com/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head>" +
            $"<body>\"duration\":{{\"seconds\":{seconds}}},\"datePublished\":\"{published}\"</body></html>");
        var sut = _mocker.CreateInstance<HuluPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
        meta.Publisher.Should().Be("Hulu");
    }

    [Fact(DisplayName =
        "Hulu catalogue hub extract sets ShowName from og:title when JSON-LD is absent on a series path, " +
        "so GET submit lookup still returns podcastName for series hubs.")]
    public async Task series_path_sets_hub_title_as_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://www.hulu.com/series/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{seriesName}\" /></head></html>");
        var sut = _mocker.CreateInstance<HuluPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seriesName);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Hulu");
    }

    [Fact(DisplayName =
        "Hulu series path keeps ShowName from the hub title even when a later Movie JSON-LD blob appears, " +
        "because carousel film markup must not clear podcastName on series catalogue pages.")]
    public async Task series_path_ignores_later_movie_blob()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var otherFilm = _fixture.CreateTitle();
        var url = new Uri($"https://www.hulu.com/series/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{seriesName}\" /></head>" +
            $"<body><div>{{\"@type\":\"Movie\",\"name\":\"{otherFilm}\"}}</div></body></html>");
        var sut = _mocker.CreateInstance<HuluPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().Be(seriesName);
    }

    [Fact(DisplayName =
        "Hulu film pages leave ShowName null even when og:title is present, " +
        "because a film has no parent series for podcastName attach.")]
    public async Task movie_path_leaves_show_name_null()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.hulu.com/movie/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{filmTitle}\" /></head></html>");
        var sut = _mocker.CreateInstance<HuluPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Hulu page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.hulu.com/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<HuluPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddHuluServices registers a catalog-keyed adapter for Hulu URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHuluServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.hulu.com/series/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.ResolveService(url).Should().Be(StreamingService.Hulu);
    }

    [Fact(DisplayName =
        "Hulu ExtractFromHtml parses Open Graph from prefetched HTML without an HTTP GET, " +
        "so Api SCRAPE_US / Browser Rendering prepare can extract after Cloudflare fetches the page.")]
    public async Task extract_from_html_parses_prefetched_catalogue_html()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.hulu.com/series/{_fixture.CreateYouTubeId()}");
        var html =
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>";
        var sut = _mocker.CreateInstance<HuluPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(url, html);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Hulu");
        meta.ShowName.Should().Be(title);
        _handler.LastRequestUri.Should().BeNull();
    }

    [Fact(DisplayName =
        "AddHuluServices registers ExtractMetaData(html) on the Hulu adapter, " +
        "so Cloudflare-prefetched catalogue HTML does not throw HTML extract is not registered.")]
    public async Task add_services_registers_html_extract_on_adapter()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.hulu.com/series/{_fixture.CreateYouTubeId()}");
        var html =
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>";
        var services = new ServiceCollection();
        services.AddHuluServices();
        using var provider = services.BuildServiceProvider();
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Act
        var meta = await adapter.ExtractMetaData(url, html);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Hulu");
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
            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}