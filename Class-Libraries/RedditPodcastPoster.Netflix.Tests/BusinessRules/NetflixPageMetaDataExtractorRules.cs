using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Netflix.Extensions;
using RedditPodcastPoster.Netflix.Extractors;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.Netflix.Tests.BusinessRules;

public class NetflixPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public NetflixPageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Netflix page extract GETs the title URL and reads Open Graph fields, " +
        "so submit can ingest a Netflix watch/title page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Netflix");
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "Netflix page extract populates ShowName from og:video:series when distinct from the episode title, " +
        "so GET submit lookup can return podcastName for unknown Netflix URLs.")]
    public async Task extracts_series_name_for_lookup()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://www.netflix.com/watch/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<meta property=\"og:video:series\" content=\"{seriesName}\" />" +
            $"</head></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Netflix");
    }

    [Fact(DisplayName =
        "Netflix series catalogue extract populates ShowName from JSON-LD TVSeries.name, " +
        "so lookup can attach by show name when the page is a title catalogue rather than a watch URL.")]
    public async Task extracts_tv_series_name_from_catalogue_json_ld()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var marketingTitle = $"Watch {seriesName} | Netflix Official Site";
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{marketingTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Netflix");
    }

    [Fact(DisplayName =
        "Netflix catalogue extract recovers TVSeries.name from raw HTML when the ld+json script is not selected by the DOM parser, " +
        "so intermittent soft-wall markup still yields podcastName for series title pages.")]
    public async Task extracts_tv_series_name_from_raw_html_fallback()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var marketingTitle = $"Watch {seriesName} | Netflix Official Site";
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        // Intentionally omit a proper script@type so OpenGraph JSON-LD walk finds nothing;
        // series payload still appears in page text (Netflix embeds ld+json with data-rh).
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{marketingTitle}\" /></head>" +
            $"<body><div>{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}</div></body></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().Be(seriesName);
    }

    [Fact(DisplayName =
        "Netflix film pages ignore unrelated TVSeries blobs in the HTML when Movie JSON-LD is absent, " +
        "so soft-walled film catalogue responses cannot attach to a recommended series name.")]
    public async Task unrelated_tv_series_blob_is_not_show_name_for_film_marketing_title()
    {
        // Arrange
        var filmName = _fixture.CreateTitle();
        var otherSeries = _fixture.CreateTitle();
        var marketingTitle = $"Watch {filmName} | Netflix Official Site";
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{marketingTitle}\" /></head>" +
            $"<body><div>{{\"@type\":\"TVSeries\",\"name\":\"{otherSeries}\"}}</div></body></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Netflix film catalogue extract keeps ShowName null when Movie JSON-LD is present, " +
        "even if a Watch … marketing og:title looks like a series catalogue title.")]
    public async Task movie_json_ld_clears_show_name()
    {
        // Arrange
        var filmName = _fixture.CreateTitle();
        var marketingTitle = $"Watch {filmName} | Netflix Official Site";
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{marketingTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Netflix soft-walled show pages recover ShowName from the h1 when type is Show and og:title is absent, " +
        "because non-member title shells still expose the series heading.")]
    public async Task soft_wall_show_uses_h1_as_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head><title>Netflix</title></head>" +
            $"<body><h1>{seriesName}</h1><script>{{\"type\":\"Show\"}}</script></body></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seriesName);
        meta.ShowName.Should().Be(seriesName);
    }

    [Fact(DisplayName =
        "Netflix soft-walled movie pages keep ShowName null and use the h1 as the film title when type is Movie.")]
    public async Task soft_wall_movie_uses_h1_without_show_name()
    {
        // Arrange
        var filmName = _fixture.CreateTitle();
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head><title>Netflix</title></head>" +
            $"<body><h1>{filmName}</h1><script>{{\"type\":\"Movie\"}}</script></body></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmName);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Netflix series catalogue keeps ShowName from the primary TVSeries even when a recommended Movie blob appears later in the HTML, " +
        "because whole-document Movie matches must not null podcastName for series title pages.")]
    public async Task series_primary_ignores_later_recommended_movie_blob()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var recommendedFilm = _fixture.CreateTitle();
        var marketingTitle = $"Watch {seriesName} | Netflix Official Site";
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{marketingTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head>" +
            $"<body><div>{{\"@type\":\"Movie\",\"name\":\"{recommendedFilm}\"}}</div></body></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.ShowName.Should().Be(seriesName);
    }

    [Fact(DisplayName =
        "Netflix soft-walled show pages keep ShowName from the h1 when primary soft-wall type is Show, " +
        "even if a recommended Movie soft-wall type appears later in the HTML.")]
    public async Task soft_wall_show_primary_ignores_later_movie_type()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head><title>Netflix</title></head>" +
            $"<body><h1>{seriesName}</h1><script>{{\"type\":\"Show\"}}</script>" +
            $"<div>{{\"type\":\"Movie\"}}</div></body></html>");
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seriesName);
        meta.ShowName.Should().Be(seriesName);
    }

    [Fact(DisplayName =
        "Netflix page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var sut = _mocker.CreateInstance<NetflixPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddNetflixServices registers a catalog-keyed adapter for Netflix URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_netflix_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNetflixServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.Netflix);
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
