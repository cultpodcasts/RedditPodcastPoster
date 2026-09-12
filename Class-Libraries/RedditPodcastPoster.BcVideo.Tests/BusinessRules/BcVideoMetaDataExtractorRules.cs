using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
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

    private static string Host => "bitchute.com";

    [Fact(DisplayName =
        "BcVideo extract reads title, description, clock duration, publish date, thumbnail, and channel from the video JSON API, " +
        "because oEmbed omits description, duration, and release even when the watch page has them.")]
    public async Task extracts_video_api_fields()
    {
        // Arrange
        var id = VideoId();
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var author = _fixture.Create<string>();
        var image = new Uri($"https://example.test/art/{_fixture.CreateYouTubeId()}");
        var extraSeconds = _fixture.Create<int>() % 59 + 1;
        var duration = _fixture.CreateDuration() + TimeSpan.FromSeconds(extraSeconds);
        var release = DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(18) + TimeSpan.FromMinutes(12));
        var url = new Uri($"https://www.{Host}/video/{id}/");
        _handler.Response = JsonResponse(new
        {
            video_name = title,
            description,
            duration = FormatClock(duration),
            date_published = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            thumbnail_url = image.ToString(),
            channel = new { channel_name = author }
        });
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
        meta.Publisher.Should().Be(author);
        meta.Image.Should().Be(image);
        meta.ShowName.Should().BeNull();
        _handler.LastRequestMethod.Should().Be(HttpMethod.Post);
        _handler.LastRequestUri.Should().NotBeNull();
        _handler.LastRequestUri!.Host.Should().Be("api.bitchute.com");
        _handler.LastRequestUri.AbsolutePath.Should().Be("/api/beta/video");
        _handler.LastRequestBody.Should().Contain(id);
    }

    [Fact(DisplayName =
        "BcVideo extract posts the compact video id from an /embed/{id} URL to the same video JSON API, " +
        "so embed pastes still receive description, duration, and release.")]
    public async Task embed_url_posts_compact_video_id()
    {
        // Arrange
        var id = VideoId();
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.{Host}/embed/{id}");
        _handler.Response = JsonResponse(new { video_name = title });
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        _handler.LastRequestUri!.AbsolutePath.Should().Be("/api/beta/video");
        using var body = JsonDocument.Parse(_handler.LastRequestBody);
        body.RootElement.GetProperty("video_id").GetString().Should().Be(id);
    }

    [Fact(DisplayName =
        "BcVideo extract parses an unpadded H:MM:SS clock duration from video JSON, " +
        "because the platform stores length as a display clock rather than integer seconds.")]
    public async Task clock_duration_with_hours_is_parsed()
    {
        // Arrange
        var duration = new TimeSpan(1, 1, 33);
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        _handler.Response = JsonResponse(new
        {
            video_name = _fixture.CreateTitle(),
            duration = "1:01:33"
        });
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(duration);
    }

    [Fact(DisplayName =
        "BcVideo extract fails when video JSON has no title, because an episode cannot be created without a title.")]
    public async Task missing_title_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        _handler.Response = JsonResponse(new { video_name = "" });
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

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

    private static string FormatClock(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}";

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; set; }
        public Uri? LastRequestUri { get; private set; }
        public HttpMethod? LastRequestMethod { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestMethod = request.Method;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return Response ?? new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }
}
