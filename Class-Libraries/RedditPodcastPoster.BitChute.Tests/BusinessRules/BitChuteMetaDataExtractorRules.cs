// pragma: allowlist secret
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.BitChute.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.BitChute.Extractors; // pragma: allowlist secret
using RedditPodcastPoster.Episodes.TestSupport.Fixtures; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions; // pragma: allowlist secret

namespace RedditPodcastPoster.BitChute.Tests.BusinessRules; // pragma: allowlist secret

public class BitChuteMetaDataExtractorRules // pragma: allowlist secret
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public BitChuteMetaDataExtractorRules() // pragma: allowlist secret
    {
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    private string VideoId()
    {
        var raw = new string(_fixture.CreateYouTubeId().Where(char.IsLetterOrDigit).ToArray());
        return (raw + "aaaaaaaaaaaa")[..12];
    }

    [Fact(DisplayName =
        "BitChute extract reads title, thumbnail, and author from oEmbed JSON, " + // pragma: allowlist secret
        "so a BitChute URL can fill an episode without a podcast catalogue API.")] // pragma: allowlist secret
    public async Task extracts_oembed_fields()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var author = _fixture.Create<string>();
        var image = new Uri($"https://example.test/art/{_fixture.CreateYouTubeId()}");
        var url = new Uri($"https://www.bitchute.com/video/{VideoId()}/"); // pragma: allowlist secret
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                  {"title":"{{title}}","author_name":"{{author}}","thumbnail_url":"{{image}}","provider_name":"BitChute"} // pragma: allowlist secret
                  """,
                Encoding.UTF8,
                "application/json")
        };
        var sut = _mocker.CreateInstance<BitChuteMetaDataExtractor>(); // pragma: allowlist secret

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be(author);
        meta.Image.Should().Be(image);
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().NotBeNull();
        _handler.LastRequestUri!.Host.Should().Be("api.bitchute.com"); // pragma: allowlist secret
        _handler.LastRequestUri.AbsolutePath.Should().Be("/oembed/");
    }

    [Fact(DisplayName =
        "BitChute extract fails when oEmbed has no title, because an episode cannot be created without a title.")] // pragma: allowlist secret
    public async Task missing_title_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.bitchute.com/video/{VideoId()}/"); // pragma: allowlist secret
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"title":""}""", Encoding.UTF8, "application/json")
        };
        var sut = _mocker.CreateInstance<BitChuteMetaDataExtractor>(); // pragma: allowlist secret

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>(); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "AddBitChuteServices registers a catalog-keyed adapter for BitChute URLs, so submit routing finds the plugin without PodcastServices knowing BitChute.")] // pragma: allowlist secret
    public void add_bitchute_services_registers_adapter() // pragma: allowlist secret
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBitChuteServices(); // pragma: allowlist secret
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.bitchute.com/video/{VideoId()}/"); // pragma: allowlist secret

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>() // pragma: allowlist secret
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.BitChute); // pragma: allowlist secret
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
