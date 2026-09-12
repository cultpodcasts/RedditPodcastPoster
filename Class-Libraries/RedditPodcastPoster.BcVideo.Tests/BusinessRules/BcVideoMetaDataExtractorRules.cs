using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        _handler.Requests.Should().ContainSingle();
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
        "BcVideo extract parses a clock duration whose minutes exceed 59 as total minutes rather than throwing, " +
        "because a malformed API clock must not 502 prepare.")]
    public async Task clock_duration_with_overflow_minutes_is_parsed()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        _handler.Response = JsonResponse(new
        {
            video_name = _fixture.CreateTitle(),
            duration = "90:00"
        });
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Duration.Should().Be(TimeSpan.FromMinutes(90));
    }

    [Fact(DisplayName =
        "When the video JSON API is forbidden, BcVideo extract falls back to oEmbed title/author plus watch-page description, " +
        "because a UK Azure outbound often cannot POST the video API even when oEmbed GET still works.")]
    public async Task video_api_forbidden_falls_back_to_oembed_and_watch_html()
    {
        // Arrange
        var id = VideoId();
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var author = _fixture.Create<string>();
        var image = new Uri($"https://example.test/art/{_fixture.CreateYouTubeId()}");
        var url = new Uri($"https://www.{Host}/video/{id}/");
        _handler.Routes.Add(request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri!.AbsolutePath == "/api/beta/video"
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                : null);
        _handler.Routes.Add(request =>
            request.Method == HttpMethod.Get &&
            request.RequestUri!.AbsolutePath == "/oembed/"
                ? JsonResponse(new
                {
                    title,
                    thumbnail_url = image.ToString(),
                    author_name = author
                })
                : null);
        _handler.Routes.Add(request =>
            request.Method == HttpMethod.Get &&
            request.RequestUri!.Host == $"www.{Host}"
                ? HtmlResponse(title, description)
                : null);
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Publisher.Should().Be(author);
        meta.Image.Should().Be(image);
        meta.Duration.Should().BeNull();
        meta.Release.Should().BeNull();
        _handler.Requests.Should().HaveCount(3);
        _handler.Requests[0].Uri!.AbsolutePath.Should().Be("/api/beta/video");
        _handler.Requests.Should().Contain(request => request.Uri!.AbsolutePath == "/oembed/");
        _handler.Requests.Should().Contain(request => request.Uri!.Host == $"www.{Host}");
        ShouldHaveLogged(LogLevel.Warning, "BcVideo video-api non-success");
        ShouldHaveLogged(LogLevel.Information, "BcVideo extract fell back to oEmbed/HTML");
    }

    [Fact(DisplayName =
        "When the video JSON API POST is canceled or times out, BcVideo extract still merges oEmbed title/author plus watch-page description, " +
        "because a hung UK Azure POST must fail over rather than wait out the full client timeout.")]
    public async Task video_api_timeout_falls_back_to_oembed_and_watch_html()
    {
        // Arrange
        var id = VideoId();
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var author = _fixture.Create<string>();
        var url = new Uri($"https://www.{Host}/video/{id}/");
        _handler.ThrowOnVideoApi = new TaskCanceledException();
        _handler.Routes.Add(request =>
            request.Method == HttpMethod.Get &&
            request.RequestUri!.AbsolutePath == "/oembed/"
                ? JsonResponse(new { title, author_name = author })
                : null);
        _handler.Routes.Add(request =>
            request.Method == HttpMethod.Get &&
            request.RequestUri!.Host == $"www.{Host}"
                ? HtmlResponse(title, description)
                : null);
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Publisher.Should().Be(author);
        meta.Duration.Should().BeNull();
        meta.Release.Should().BeNull();
        ShouldHaveLogged(LogLevel.Warning, "BcVideo video-api request failed");
        ShouldHaveLogged(LogLevel.Information, "BcVideo extract fell back to oEmbed/HTML");
    }

    [Fact(DisplayName =
        "When falling back from an /embed/{id} URL, oEmbed is queried with the canonical /video/{id} watch URL, " +
        "because oEmbed 404s on embed paths.")]
    public async Task embed_fallback_oembed_uses_canonical_watch_url()
    {
        // Arrange
        var id = VideoId();
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.{Host}/embed/{id}");
        _handler.Routes.Add(request =>
            request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                : null);
        _handler.Routes.Add(request =>
            request.Method == HttpMethod.Get &&
            request.RequestUri!.AbsolutePath == "/oembed/"
                ? JsonResponse(new { title, author_name = _fixture.Create<string>() })
                : null);
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        var oEmbed = _handler.Requests.Single(request => request.Uri!.AbsolutePath == "/oembed/");
        var query = Uri.UnescapeDataString(oEmbed.Uri!.Query);
        query.Should().Contain($"/video/{id}");
        query.Should().NotContain($"/embed/{id}");
    }

    [Fact(DisplayName =
        "BcVideo HTML extract reads a posted video JSON body without calling the host, " +
        "so the Worker can prefetch the video API from a non-UK network and Azure can map duration and release.")]
    public async Task extract_from_html_parses_video_api_json()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var author = _fixture.Create<string>();
        var duration = new TimeSpan(1, 1, 33);
        var release = DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(18) + TimeSpan.FromMinutes(12));
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        var html = JsonSerializer.Serialize(new
        {
            video_name = title,
            description,
            duration = "1:01:33",
            date_published = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            channel = new { channel_name = author }
        });
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url, html);

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
        meta.Publisher.Should().Be(author);
        _handler.Requests.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "BcVideo extract reads a posted oEmbed JSON body without calling the host, " +
        "so a Worker-prefetched oEmbed payload still yields title and author.")]
    public async Task extract_from_html_parses_oembed_json()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var author = _fixture.Create<string>();
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        var html = JsonSerializer.Serialize(new { title, author_name = author });
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url, html);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be(author);
        meta.Duration.Should().BeNull();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "BcVideo HTML extract reads og:title and description from a watch-page shell, " +
        "because the SPA HTML has those meta tags even when duration and release are absent.")]
    public async Task extract_from_html_parses_watch_page_og_tags()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url, WatchHtml(title, description));

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Duration.Should().BeNull();
        meta.Release.Should().BeNull();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "BcVideo extract fails when video JSON, oEmbed, and watch HTML all omit a title, because an episode cannot be created without a title.")]
    public async Task missing_title_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        _handler.Routes.Add(_ => JsonResponse(new { video_name = "" }));
        var sut = _mocker.CreateInstance<BcVideoMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddBcVideoServices registers a catalog-keyed adapter that can extract from posted HTML/JSON, " +
        "so Worker-prefetched video JSON is accepted on SubmitUrl/extract.")]
    public async Task add_bc_video_services_registers_html_extract()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<BcVideoMetaDataExtractor>>(NullLogger<BcVideoMetaDataExtractor>.Instance);
        services.AddBcVideoServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.{Host}/video/{VideoId()}/");
        var title = _fixture.CreateTitle();
        var html = JsonSerializer.Serialize(new { video_name = title });

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));
        var meta = await adapter.ExtractMetaData(url, html);

        // Assert
        adapter.Service.Should().Be(NonPodcastService.BcVideo);
        adapter.CanExtract(url).Should().BeTrue();
        meta.Title.Should().Be(title);
    }

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage HtmlResponse(string title, string description) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(WatchHtml(title, description), Encoding.UTF8, "text/html")
        };

    private static string WatchHtml(string title, string description)
    {
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedDescription = WebUtility.HtmlEncode(description);
        return $"<html><head><title>{encodedTitle}</title>" +
               $"<meta name=\"description\" content=\"{encodedDescription}\" />" +
               $"<meta property=\"og:title\" content=\"{encodedTitle}\" />" +
               $"<meta property=\"og:description\" content=\"{encodedDescription}\" /></head><body></body></html>";
    }

    private static string FormatClock(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}";

    private void ShouldHaveLogged(LogLevel level, string fragment)
    {
        _mocker.GetMock<ILogger<BcVideoMetaDataExtractor>>().Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(fragment)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly object _gate = new();
        public HttpResponseMessage? Response { get; set; }
        public Exception? ThrowOnVideoApi { get; set; }
        public List<Func<HttpRequestMessage, HttpResponseMessage?>> Routes { get; } = [];
        public List<(HttpMethod Method, Uri? Uri, string Body)> Requests { get; } = [];
        public Uri? LastRequestUri { get; private set; }
        public HttpMethod? LastRequestMethod { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (_gate)
            {
                LastRequestUri = request.RequestUri;
                LastRequestMethod = request.Method;
                LastRequestBody = body;
                Requests.Add((request.Method, request.RequestUri, body));
            }

            if (ThrowOnVideoApi != null &&
                request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath == "/api/beta/video")
            {
                throw ThrowOnVideoApi;
            }

            foreach (var route in Routes)
            {
                var mapped = route(request);
                if (mapped != null)
                {
                    return mapped;
                }
            }

            return Response ?? new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }
}
