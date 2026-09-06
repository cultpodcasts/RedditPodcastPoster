using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PlayRts.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.PlayRts.Extractors; // pragma: allowlist secret
using RedditPodcastPoster.Episodes.TestSupport.Fixtures; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using RedditPodcastPoster.OpenGraph.Extractors; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions; // pragma: allowlist secret

namespace RedditPodcastPoster.PlayRts.Tests.BusinessRules; // pragma: allowlist secret

public class PlayRtsPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public PlayRtsPageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Play RTS episode extract prefers JSON-LD TVEpisode name, partOfSeries show name, ISO duration with years/months, and uploadDate, " +
        "so submit does not keep the og:title show-name suffix or fail XmlConvert duration parse.")]
    public async Task extracts_tv_episode_json_ld_over_og_suffix()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var seriesName = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcAtTime(-3, duration);
        var show = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}/video/{episode}");
        var iso = $"P0Y0M0DT{duration.Hours}H{duration.Minutes}M{duration.Seconds}S";
        var upload = release.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle} - {seriesName} - Play RTS\" />" +
            "<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVEpisode\",\"name\":\"{episodeTitle}\"," +
            $"\"duration\":\"{iso}\",\"uploadDate\":\"{upload}\"," +
            $"\"partOfSeries\":{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}}}" +
            "</script></head></html>");
        var sut = _mocker.CreateInstance<PlayRtsPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Duration.Should().Be(duration);
        meta.Release.Should().Be(release);
        meta.Publisher.Should().Be("Play RTS");
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "Play RTS series hub extract uses the document title as ShowName when JSON-LD has no partOfSeries, " +
        "because /play/tv/{slug} is a catalogue series page rather than a movie.")]
    public async Task series_hub_without_part_of_series_uses_title_as_show_name()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var show = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title} - Play RTS\" /></head></html>");
        var sut = _mocker.CreateInstance<PlayRtsPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.ShowName.Should().Be(title);
        meta.Publisher.Should().Be("Play RTS");
    }

    [Fact(DisplayName =
        "Play RTS film pages leave ShowName null when og:type is a movie, because a film has no parent series for podcastName attach.")] // pragma: allowlist secret
    public async Task movie_pages_do_not_set_show_name()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var show = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{filmTitle}\" />" +
            "<meta property=\"og:type\" content=\"video.movie\" />" +
            "</head></html>");
        var sut = _mocker.CreateInstance<PlayRtsPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Play RTS page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var show = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<PlayRtsPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>(); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "AddPlayRtsServices registers a catalog-keyed adapter for Play RTS URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_playrts_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPlayRtsServices();
        using var provider = services.BuildServiceProvider();
        var show = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>() // pragma: allowlist secret
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.PlayRts); // pragma: allowlist secret
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
