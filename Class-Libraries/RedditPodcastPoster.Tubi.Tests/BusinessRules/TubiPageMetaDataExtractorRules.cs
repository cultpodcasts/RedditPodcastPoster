using System.Net;
using System.Text;
using System.Xml;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.Tubi.Extensions;
using RedditPodcastPoster.Tubi.Extractors;

namespace RedditPodcastPoster.Tubi.Tests.BusinessRules;

public class TubiPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public TubiPageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Tubi page extract GETs the catalogue URL and reads Open Graph fields, " +
        "so submit can ingest a Tubi watch/title page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Tubi");
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "Tubi film pages leave ShowName null even when JSON-LD names the Movie, " +
        "because a film has no parent series for podcastName attach.")]
    public async Task movie_pages_do_not_set_show_name()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://tubitv.com/en-au/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{filmTitle}\" />" +
            $"<meta property=\"og:type\" content=\"video.movie\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmTitle}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
        meta.Publisher.Should().Be("Tubi");
    }

    [Fact(DisplayName =
        "Tubi series extract populates ShowName from JSON-LD TVSeries, " +
        "so GET submit lookup can return podcastName for a series catalogue page.")]
    public async Task extracts_tvseries_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var url = new Uri($"https://tubitv.com/tv-shows/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Tubi");
    }

    [Fact(DisplayName =
        "Tubi catalogue hub extract sets ShowName from og:title when JSON-LD TVSeries is absent, " +
        "so GET submit lookup still returns podcastName for a series path.")]
    public async Task extracts_hub_title_as_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://tubitv.com/tv-shows/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{seriesName}\" /></head></html>");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seriesName);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Tubi");
    }

    [Fact(DisplayName =
        "Tubi extract reads video:duration integer seconds when JSON-LD duration is absent, " +
        "so prepare still gets a length for a film page.")]
    public async Task extracts_video_duration_seconds()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{title}\" />" +
            $"<meta property=\"video:duration\" content=\"6124\" />" +
            $"</head></html>");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(TimeSpan.FromSeconds(6124));
    }

    [Fact(DisplayName =
        "Tubi extract fills Release from date-only video:release_date (yyyy-MM-dd, no time), " +
        "because live catalogue head uses that production format.")]
    public async Task extracts_date_only_video_release_date()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var release = DomainTestFixture.UtcDateDaysAgo(40);
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(
            url,
            $"<html><head>" +
            Meta("og:title", title) +
            Meta("video:release_date", release.ToString("yyyy-MM-dd")) +
            "</head></html>");

        // Assert
        meta.Release.Should().Be(release);
        meta.Publisher.Should().Be("Tubi");
        _handler.LastRequestUri.Should().BeNull();
    }

    [Fact(DisplayName =
        "Tubi extract fills Release from JSON-LD dateCreated when video:release_date is absent, " +
        "so prepare still gets a date from the catalogue script.")]
    public async Task extracts_json_ld_date_created_when_video_release_date_absent()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var release = DomainTestFixture.UtcDateDaysAgo(80);
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(
            url,
            $"<html><head>" +
            Meta("og:title", title) +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"dateCreated\":\"{release:yyyy-MM-dd}\"}}" +
            $"</script></head></html>");

        // Assert
        meta.Release.Should().Be(release);
        _handler.LastRequestUri.Should().BeNull();
    }

    [Fact(DisplayName =
        "Tubi extract fills Duration from JSON-LD ISO duration when video:duration is absent, " +
        "so prepare still gets a length from the catalogue script.")]
    public async Task extracts_iso_duration_when_video_duration_absent()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(
            url,
            $"<html><head><title>{WebUtility.HtmlEncode(title)} | Tubi</title>" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"duration\":\"{XmlConvert.ToString(duration)}\"}}" +
            $"</script></head></html>");

        // Assert
        meta.Duration.Should().Be(duration);
        _handler.LastRequestUri.Should().BeNull();
    }

    [Fact(DisplayName =
        "Tubi extract fills title, description, image, duration, and release from catalogue head meta, " +
        "so prepare can create a film episode without a podcast catalogue API.")]
    public async Task extracts_title_description_image_duration_and_release()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = $"{_fixture.CreateTitle()} it's {_fixture.CreateTitle()}";
        var image = new Uri($"https://example.test/poster/{_fixture.CreateYouTubeId()}.jpg");
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 365 + 1), TimeSpan.FromHours(14));
        var url = new Uri($"https://tubitv.com/en-au/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(TubiCatalogueHead(
            Meta("og:title", title),
            Meta("og:type", "video.movie"),
            Meta("og:description", description),
            Meta("og:image", image.ToString()),
            Meta("video:duration", ((int)duration.TotalSeconds).ToString()),
            Meta("video:release_date", release.ToString("yyyy-MM-ddTHH:mm:ssZ"))));
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Image.Should().Be(image);
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
        meta.ShowName.Should().BeNull();
        meta.Publisher.Should().Be("Tubi");
    }

    [Fact(DisplayName =
        "Tubi HTML recovery fills title, description, image, duration, and release when og:title is absent, " +
        "so Worker-posted catalogue HTML still maps after Open Graph extract fails.")]
    public async Task html_recovery_fills_catalogue_fields_when_open_graph_omits_title()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.CreateTitle();
        var image = new Uri($"https://example.test/poster/{_fixture.CreateYouTubeId()}.jpg");
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 365 + 1), TimeSpan.FromHours(12));
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(
            url,
            $"<html><head><title>{WebUtility.HtmlEncode(title)} | Tubi</title>" +
            Meta("og:description", description) +
            Meta("og:image", image.ToString()) +
            Meta("video:duration", ((int)duration.TotalSeconds).ToString()) +
            Meta("video:release_date", release.ToString("yyyy-MM-ddTHH:mm:ssZ")) +
            "</head></html>");

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Image.Should().Be(image);
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
        meta.Publisher.Should().Be("Tubi");
        _handler.LastRequestUri.Should().BeNull();
    }

    [Fact(DisplayName =
        "Tubi HTML extract reads og:title from posted HTML without calling the host, " +
        "so the Worker can POST prefetched catalogue HTML to Azure extract.")]
    public async Task html_extract_does_not_call_host()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(
            url,
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Tubi");
        _handler.LastRequestUri.Should().BeNull();
    }

    [Fact(DisplayName =
        "Tubi page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<TubiPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddTubiServices registers a catalog-keyed adapter that can extract from posted HTML, " +
        "so Worker-prefetched catalogue HTML maps without a second Azure GET.")]
    public async Task add_tubi_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTubiServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://tubitv.com/movies/{_fixture.CreateAppleId()}/{_fixture.CreateYouTubeId()}");
        var title = _fixture.CreateTitle();
        var html = $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>";

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));
        var meta = await adapter.ExtractMetaData(url, html);

        // Assert
        adapter.ResolveService(new Uri("https://example.com/")).Should().Be(StreamingService.Tubi);
        adapter.CanExtract(url).Should().BeTrue();
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Tubi");
        _handler.LastRequestUri.Should().BeNull();
    }

    private static HttpResponseMessage OkHtml(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

    private static string TubiCatalogueHead(params string[] metas) =>
        "<html><head>" + string.Concat(metas) + "</head></html>";

    private static string Meta(string property, string content) =>
        $"<meta property=\"{property}\" content=\"{WebUtility.HtmlEncode(content)}\" data-tubi-ssr=\"\"/>";

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
