using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Arte.Extensions;
using RedditPodcastPoster.Arte.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.Arte.Tests.BusinessRules;

public class ArtePageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public ArtePageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "An ARTE collection page uses the brand before the theme dash as both title and ShowName, " +
        "and drops the | ARTE suffix, so a multi-language collection hub attaches as the series.")]
    public async Task collection_page_uses_brand_before_theme()
    {
        // Arrange
        var brand = _fixture.CreateTitle();
        var theme = _fixture.CreateTitle();
        var url = CollectionUrl();
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{brand} - {theme} | ARTE\" /></head></html>");
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(brand);
        meta.ShowName.Should().Be(brand);
        meta.Publisher.Should().Be("ARTE");
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "An ARTE programme that lists a parent collection strips the language-specific watch call-to-action " +
        "and sets ShowName to the series brand, so English, French, and German pages attach to the same series.")]
    public async Task programme_with_parent_collection_sets_show_name()
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var episode = _fixture.CreateTitle();
        var collectionId = _fixture.CreateAppleId();
        var url = ProgrammeUrl();
        var ogTitle = $"{series} - {episode} - Watch the full documentary | ARTE in English";
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{ogTitle}\" /></head>" +
            $"<body>associatedCollections\\\":[\\\"RC-{collectionId}\\\"]</body></html>");
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be($"{series} - {episode}");
        meta.ShowName.Should().Be(series);
        meta.Publisher.Should().Be("ARTE");
    }

    [Fact(DisplayName =
        "An ARTE programme with an empty associatedCollections array leaves ShowName null after stripping the watch call-to-action, " +
        "because a film or standalone has no parent series.")]
    public async Task standalone_programme_leaves_show_name_null()
    {
        // Arrange
        var film = _fixture.CreateTitle();
        var url = ProgrammeUrl();
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{film} - Ver la película completa | ARTE en español\" /></head>" +
            "<body>associatedCollections\\\":[]</body></html>");
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(film);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "An ARTE programme strips German and French watch call-to-actions the same way as English, " +
        "so Die ganze Doku and Regarder le documentaire complet are not stored as the title.")]
    public async Task strips_german_and_french_watch_calls_to_action()
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var episode = _fixture.CreateTitle();
        var collectionId = _fixture.CreateAppleId();
        var url = ProgrammeUrl();
        var german = $"{series} - {episode} - Die ganze Doku | ARTE";
        var french = $"{series} - {episode} - Regarder le documentaire complet | ARTE";
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        _handler.Response = OkHtml(ProgrammeHtml(german, collectionId));
        var germanMeta = await sut.GetMetaData(url);
        _handler.Response = OkHtml(ProgrammeHtml(french, collectionId));
        var frenchMeta = await sut.GetMetaData(url);

        // Assert
        germanMeta.Title.Should().Be($"{series} - {episode}");
        germanMeta.ShowName.Should().Be(series);
        frenchMeta.Title.Should().Be($"{series} - {episode}");
        frenchMeta.ShowName.Should().Be(series);
    }

    [Fact(DisplayName =
        "An ARTE programme recovers Duration and Release from embedded player duration.seconds and rights.begin " +
        "when Open Graph omits them, because Arte pages put that metadata in the Next.js freight instead of og tags.")]
    public async Task programme_recovers_duration_and_release_from_embedded_player()
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var episode = _fixture.CreateTitle();
        var collectionId = _fixture.CreateAppleId();
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var seconds = (int)duration.TotalSeconds;
        var begin = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var url = ProgrammeUrl();
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{series} - {episode} | ARTE\" /></head>" +
            $"<body>associatedCollections\\\":[\\\"RC-{collectionId}\\\"]" +
            $"serverSideTracking\\\":{{\\\"duration\\\":{{\\\"seconds\\\":{seconds}}}," +
            $"\\\"rights\\\":{{\\\"begin\\\":\\\"{begin}\\\"}}}}</body></html>");
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
    }

    [Fact(DisplayName =
        "An ARTE programme recovers Duration from a raw embedded duration seconds int and Release from datePublished " +
        "when the player object form is absent.")]
    public async Task programme_recovers_raw_duration_seconds_and_date_published()
    {
        // Arrange
        var film = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var seconds = (int)duration.TotalSeconds;
        var published = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var url = ProgrammeUrl();
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{film} | ARTE\" /></head>" +
            $"<body>\"duration\":{seconds},\"datePublished\":\"{published}\"</body></html>");
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
    }

    [Fact(DisplayName =
        "An ARTE programme prefers Open Graph JSON-LD Duration and Release over embedded player seconds and begin, " +
        "so a structured ld+json payload wins when both are present.")]
    public async Task programme_prefers_open_graph_duration_and_release_over_html_embed()
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var episode = _fixture.CreateTitle();
        var collectionId = _fixture.CreateAppleId();
        var ogDuration = _fixture.CreateDuration();
        var htmlDuration = ogDuration + TimeSpan.FromMinutes(7);
        var ogRelease = DomainTestFixture.UtcAtTime(-6, _fixture.CreateNonMidnightTimeOfDay());
        var htmlRelease = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());
        var iso = $"PT{ogDuration.Hours}H{ogDuration.Minutes}M{ogDuration.Seconds}S";
        var published = ogRelease.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var begin = htmlRelease.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var url = ProgrammeUrl();
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{series} - {episode} | ARTE\" />" +
            "<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"VideoObject\",\"duration\":\"{iso}\",\"datePublished\":\"{published}\"}}" +
            "</script></head>" +
            $"<body>associatedCollections\\\":[\\\"RC-{collectionId}\\\"]" +
            $"\"duration\":{((int)htmlDuration.TotalSeconds)},\"begin\":\"{begin}\"</body></html>");
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(ogDuration);
        meta.Release.Should().Be(ogRelease);
    }

    [Fact(DisplayName =
        "An ARTE collection hub without programme duration or release embeds leaves Duration and Release null, " +
        "because a multi-programme RC- page has no single episode length.")]
    public async Task collection_hub_without_programme_meta_leaves_duration_and_release_null()
    {
        // Arrange
        var brand = _fixture.CreateTitle();
        var theme = _fixture.CreateTitle();
        var url = CollectionUrl();
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{brand} - {theme} | ARTE\" /></head></html>");
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().BeNull();
        meta.Release.Should().BeNull();
    }

    [Fact(DisplayName =
        "ARTE page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = ProgrammeUrl();
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<ArtePageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddArteServices registers a catalog-keyed adapter for ARTE URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddArteServices();
        using var provider = services.BuildServiceProvider();
        var url = CollectionUrl();

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.ResolveService(new Uri("https://example.com/")).Should().Be(StreamingService.Arte);
    }

    private Uri CollectionUrl()
    {
        var id = _fixture.CreateAppleId();
        var slug = _fixture.CreateYouTubeId();
        return new Uri($"https://www.arte.tv/fr/videos/RC-{id}/{slug}/");
    }

    private Uri ProgrammeUrl()
    {
        var head = _fixture.CreateAppleId().ToString();
        var tail = _fixture.CreateAppleId().ToString();
        var slug = _fixture.CreateYouTubeId();
        return new Uri($"https://www.arte.tv/en/videos/{head[..6]}-{tail[..3]}-A/{slug}/");
    }

    private static string ProgrammeHtml(string ogTitle, long collectionId) =>
        $"<html><head><meta property=\"og:title\" content=\"{ogTitle}\" /></head>" +
        $"<body>associatedCollections\\\":[\\\"RC-{collectionId}\\\"]</body></html>";

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
