using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Peacock.Extensions;
using RedditPodcastPoster.Peacock.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.Peacock.Tests.BusinessRules;

public class PeacockPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public PeacockPageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Peacock page extract GETs the catalogue URL and reads Open Graph fields, " +
        "so submit can ingest a Peacock page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.peacocktv.com/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<PeacockPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Peacock");
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "Peacock page extract fills Duration and Release from JSON-LD VideoObject ISO duration and datePublished, " +
        "so submit can store episode length and air date from the catalogue page.")]
    public async Task extracts_duration_and_release_from_json_ld()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-5, _fixture.CreateNonMidnightTimeOfDay());
        var iso = $"PT{duration.Hours}H{duration.Minutes}M{duration.Seconds}S";
        var published = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var url = new Uri($"https://www.peacocktv.com/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{title}\" />" +
            "<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"VideoObject\",\"duration\":\"{iso}\",\"datePublished\":\"{published}\"}}" +
            "</script></head></html>");
        var sut = _mocker.CreateInstance<PeacockPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
    }

        [Fact(DisplayName =
        "Peacock page extract recovers Duration and Release from embedded duration.seconds and datePublished " +
        "when Open Graph omits them, so prepare stores timing the same way as a JSON-LD catalogue page.")]
    public async Task recovers_duration_and_release_from_embedded_html_when_og_omits_them()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var seconds = (int)duration.TotalSeconds;
        var published = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var url = new Uri($"https://www.peacocktv.com/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head>" +
            $"<body>\"duration\":{{\"seconds\":{seconds}}},\"datePublished\":\"{published}\"</body></html>");
        var sut = _mocker.CreateInstance<PeacockPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
        meta.Publisher.Should().Be("Peacock");
    }

[Fact(DisplayName =
        "Peacock page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.peacocktv.com/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<PeacockPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddPeacockServices registers a catalog-keyed adapter for Peacock URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPeacockServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.peacocktv.com/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.ResolveService(url).Should().Be(StreamingService.Peacock);
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