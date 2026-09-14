using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.FranceTv.Extensions;
using RedditPodcastPoster.FranceTv.Extractors;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.FranceTv.Tests.BusinessRules;

public class FranceTvPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public FranceTvPageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "France TV page extract GETs the catalogue URL and reads Open Graph fields, " +
        "so submit can ingest a France TV page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var channel = _fixture.CreateYouTubeId();
        var show = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.france.tv/{channel}/{show}/");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<FranceTvPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("France TV");
        meta.ShowName.Should().Be(title);
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "France TV episode extract sets ShowName from BreadcrumbList when Open Graph omits series, " +
        "so prepare can attach the parent brand for podcastName.")]
    public async Task episode_breadcrumb_sets_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var channel = _fixture.CreateYouTubeId();
        var show = _fixture.CreateYouTubeId();
        var url = new Uri(
            $"https://www.france.tv/{channel}/{show}/{_fixture.CreateAppleId()}-{_fixture.CreateYouTubeId()}.html");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"BreadcrumbList\",\"itemListElement\":[" +
            $"{{\"@type\":\"ListItem\",\"position\":1,\"name\":\"france.tv\"}}," +
            $"{{\"@type\":\"ListItem\",\"position\":2,\"name\":\"france tv slash\"}}," +
            $"{{\"@type\":\"ListItem\",\"position\":3,\"name\":\"{seriesName}\"}}" +
            $"]}}</script></head></html>");
        var sut = _mocker.CreateInstance<FranceTvPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("France TV");
    }

    [Fact(DisplayName =
        "France TV film pages leave ShowName null even when a breadcrumb brand is present, " +
        "because a film has no parent series for podcastName attach.")]
    public async Task movie_pages_do_not_set_show_name()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var channel = _fixture.CreateYouTubeId();
        var show = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.france.tv/{channel}/{show}/");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{filmTitle}\" />" +
            $"<meta property=\"og:type\" content=\"video.movie\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"BreadcrumbList\",\"itemListElement\":[" +
            $"{{\"@type\":\"ListItem\",\"position\":1,\"name\":\"{filmTitle}\"}}" +
            $"]}}</script></head></html>");
        var sut = _mocker.CreateInstance<FranceTvPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "France TV page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri(
            $"https://www.france.tv/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<FranceTvPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddFranceTvServices registers a catalog-keyed adapter for France TV URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFranceTvServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri(
            $"https://www.france.tv/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.FranceTv);
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
