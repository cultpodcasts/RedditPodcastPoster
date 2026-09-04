using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Channel4.Extensions;
using RedditPodcastPoster.Channel4.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.Channel4.Tests.BusinessRules;

public class Channel4PageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public Channel4PageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Channel 4 page extract GETs the programme URL and reads Open Graph fields, " +
        "so submit can ingest a Channel 4 watch/programme page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<Channel4PageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Channel 4");
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "Channel 4 series extract populates ShowName from brandTitle even when it matches og:title, " +
        "so GET submit lookup can return podcastName for a programme hub.")]
    public async Task extracts_brand_title_as_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta name=\"og:title\" content=\"{seriesName}\" />" +
            $"<meta name=\"brandTitle\" content=\"{seriesName}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<Channel4PageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seriesName);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Channel 4");
    }

    [Fact(DisplayName =
        "Channel 4 film pages leave ShowName null even when a brandTitle is present, " +
        "because a film has no parent series for podcastName attach.")]
    public async Task movie_pages_do_not_set_show_name()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta name=\"og:title\" content=\"{filmTitle}\" />" +
            $"<meta name=\"brandTitle\" content=\"{filmTitle}\" />" +
            $"<meta name=\"og:type\" content=\"video.movie\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmTitle}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<Channel4PageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Channel 4 page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<Channel4PageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddChannel4Services registers a catalog-keyed adapter for Channel 4 URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_channel4_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannel4Services();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.Channel4);
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
