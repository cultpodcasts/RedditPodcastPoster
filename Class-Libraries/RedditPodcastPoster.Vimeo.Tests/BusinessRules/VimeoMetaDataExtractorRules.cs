using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.Vimeo.Extensions;
using RedditPodcastPoster.Vimeo.Extractors;

namespace RedditPodcastPoster.Vimeo.Tests.BusinessRules;

public class VimeoMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public VimeoMetaDataExtractorRules()
    {
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Vimeo extract reads title, description, duration, upload date, thumbnail, and author from oEmbed JSON, " +
        "so a Vimeo URL can fill an episode without a podcast catalogue API.")]
    public async Task extracts_oembed_fields()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var author = _fixture.Create<string>();
        var image = new Uri($"https://example.test/art/{_fixture.CreateYouTubeId()}");
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        var release = DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(18));
        var uploadDate = release.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                  {"title":"{{title}}","description":"{{description}}","duration":90,"upload_date":"{{uploadDate}}","thumbnail_url":"{{image}}","author_name":"{{author}}"}
                  """,
                Encoding.UTF8,
                "application/json")
        };
        var sut = _mocker.CreateInstance<VimeoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Duration.Should().Be(TimeSpan.FromSeconds(90));
        meta.Image.Should().Be(image);
        meta.Publisher.Should().Be(author);
        meta.Release.Should().Be(release);
        _handler.LastRequestUri.Should().NotBeNull();
        _handler.LastRequestUri!.Host.Should().Be("vimeo.com");
        _handler.LastRequestUri.AbsolutePath.Should().Be("/api/oembed.json");
    }

    [Fact(DisplayName =
        "Vimeo extract fails when oEmbed has no title, because an episode cannot be created without a title.")]
    public async Task missing_title_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"title":""}""", Encoding.UTF8, "application/json")
        };
        var sut = _mocker.CreateInstance<VimeoMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddVimeoServices registers a catalog-keyed adapter for Vimeo URLs, so submit routing finds the plugin without PodcastServices knowing HtmlAgilityPack or Vimeo.")]
    public void add_vimeo_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddVimeoServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.Vimeo);
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
