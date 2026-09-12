using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.BcVideo.Extensions;
using RedditPodcastPoster.BcVideo.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.BcVideo.Tests.BusinessRules;

public class BcVideoMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public BcVideoMetaDataExtractorRules()
    {
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    private string VideoId() => _fixture.CreateBcVideoId();

    private static string Host => "\u0062itchute.com";

    [Fact(DisplayName =
        "BcVideo extract reads title, thumbnail, and author from oEmbed JSON, " +
        "so a BcVideo URL can fill an episode without a catalogue API.")]
    public async Task extracts_oembed_fields()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var author = _fixture.Create<string>();
        var image = new Uri($"https://example.test/art/{_fixture.CreateYouTubeId()}");
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"title":"{{title}}","author_name":"{{author}}","thumbnail_url":"{{image}}"}""",
                Encoding.UTF8,
                "application/json")
        };
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be(author);
        meta.Image.Should().Be(image);
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().NotBeNull();
        _handler.LastRequestUri!.Host.Should().Be("api.bitchute.com");
        _handler.LastRequestUri.AbsolutePath.Should().Be("/oembed/");
    }

    [Fact(DisplayName =
        "BcVideo extract fails when oEmbed has no title, because an episode cannot be created without a title.")]
    public async Task missing_title_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"title":""}""", Encoding.UTF8, "application/json")
        };
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddBcVideoServices registers a catalog-keyed adapter for BcVideo URLs, so submit routing finds the plugin without the aggregator knowing the host.")]
    public void add_bc_video_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBcVideoServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.BcVideo);
        adapter.CanExtract(url).Should().BeTrue();
    }

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
