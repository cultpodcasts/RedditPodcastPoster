using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PlaySuisse.Extensions;
using RedditPodcastPoster.PlaySuisse.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.PlaySuisse.Tests.BusinessRules;

public class PlaySuissePageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public PlaySuissePageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Play Suisse page extract GETs the catalogue URL and reads Open Graph fields, " +
        "so submit can ingest a Play Suisse watch/title page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.playsuisse.ch/watch/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<PlaySuissePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Play Suisse");
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "Play Suisse /watch/{id} pages without series-path or TVSeries evidence keep ShowName null, " +
        "because numeric watch one-offs must not treat the title as a parent series brand.")]
    public async Task watch_one_off_without_series_evidence_keeps_show_name_null()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.playsuisse.ch/watch/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<PlaySuissePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.ShowName.Should().BeNull();
        meta.Publisher.Should().Be("Play Suisse");
    }

    [Fact(DisplayName =
        "Play Suisse series extract populates ShowName from JSON-LD TVSeries, " +
        "so GET submit lookup can return podcastName for a series catalogue page.")]
    public async Task extracts_tvseries_show_name()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.playsuisse.ch/watch/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<PlaySuissePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Play Suisse");
    }

    [Fact(DisplayName =
        "Play Suisse film pages leave ShowName null even when a TVSeries-looking brand is present, " +
        "because a film has no parent series for podcastName attach.")]
    public async Task movie_pages_do_not_set_show_name()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.playsuisse.ch/watch/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{filmTitle}\" />" +
            $"<meta property=\"og:type\" content=\"video.movie\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{filmTitle}\"}}" +
            $"</script></head></html>");
        var sut = _mocker.CreateInstance<PlaySuissePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Play Suisse page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.playsuisse.ch/watch/{_fixture.CreateAppleId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<PlaySuissePageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddPlaySuisseServices registers a catalog-keyed adapter for Play Suisse URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_playsuisse_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPlaySuisseServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.playsuisse.ch/watch/{_fixture.CreateAppleId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.PlaySuisse);
    }


    [Fact(DisplayName =
        "Play Suisse season-hub extract uses the first TVEpisode name, firstEpisodeDuration seconds, and JSON-LD image, " +
        "so submit does not keep the Saison/Série og:title, a zero duration, a year-only 1 Jan midnight release, or the templated og:image poster.")]
    public async Task season_hub_uses_first_episode_title_duration_and_json_ld_image()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var seconds = (int)duration.TotalSeconds;
        var posterId = _fixture.CreateYouTubeId();
        var heroId = _fixture.CreateYouTubeId();
        var year = DomainTestFixture.UtcToday.Year;
        var url = new Uri($"https://www.playsuisse.ch/detail/{_fixture.CreateAppleId()}");
        var ogImage = $"https://playsuisse-img.akamaized.net/Service.svc/GetImage/p/1/entry_id/{posterId}/version/0?imwidth={{width}}";
        var jsonLdImage = $"https://playsuisse-img.akamaized.net/Service.svc/GetImage/p/1/entry_id/{heroId}/version/0?imwidth=1200&w=1200";
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{seriesName} - Saison 1 - Série | Play Suisse\" />" +
            $"<meta property=\"og:image\" content=\"{ogImage}\" />" +
            "<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"," +
            $"\"datePublished\":\"{year}-01-01T00:00:00.000Z\"," +
            $"\"image\":\"{jsonLdImage}\"," +
            $"\"episode\":[{{\"@type\":\"TVEpisode\",\"name\":\"{episodeTitle}\",\"episodeNumber\":1}}]}}" +
            "</script></head>" +
            $"<body>\\\"firstEpisodeDuration\\\":\\\"{seconds}\\\"</body></html>");
        var sut = _mocker.CreateInstance<PlaySuissePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Duration.Should().Be(TimeSpan.FromSeconds(seconds));
        meta.Release.Should().BeNull();
        meta.Image.Should().Be(new Uri(jsonLdImage));
        meta.Publisher.Should().Be("Play Suisse");
    }

    [Fact(DisplayName =
        "Play Suisse /watch extract keeps the watch og:title and does not take nested episode-1 duration or drop a year-only release, " +
        "because hub rewrite is gated on /detail or /show catalogue paths rather than any firstEpisodeDuration or TVEpisode episodeNumber 1 blob.")]
    public async Task watch_page_with_nested_episode_one_keeps_watch_title()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var watchTitle = _fixture.CreateTitle();
        var episodeOneTitle = _fixture.CreateTitle();
        var duration = _fixture.CreateDuration();
        var seconds = (int)duration.TotalSeconds;
        var year = DomainTestFixture.UtcToday.Year;
        var yearOnlyRelease = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var url = new Uri($"https://www.playsuisse.ch/watch/{_fixture.CreateAppleId()}");
        _handler.Response = OkHtml(
            "<html><head>" +
            $"<meta property=\"og:title\" content=\"{watchTitle}\" />" +
            "<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"," +
            $"\"datePublished\":\"{year}-01-01T00:00:00.000Z\"," +
            $"\"episode\":[{{\"@type\":\"TVEpisode\",\"name\":\"{episodeOneTitle}\",\"episodeNumber\":1}}]}}" +
            "</script></head>" +
            $"<body>\\\"firstEpisodeDuration\\\":\\\"{seconds}\\\"</body></html>");
        var sut = _mocker.CreateInstance<PlaySuissePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(watchTitle);
        meta.Title.Should().NotBe(episodeOneTitle);
        meta.Duration.Should().BeNull();
        meta.Release.Should().Be(yearOnlyRelease);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Play Suisse");
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
