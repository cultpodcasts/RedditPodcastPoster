using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.AmazonPrime.Extensions;
using RedditPodcastPoster.AmazonPrime.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.AmazonPrime.Tests.BusinessRules;

public class AmazonPrimePageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public AmazonPrimePageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "Prime Video page extract GETs the detail URL and reads Open Graph fields, " +
        "so submit can ingest a Prime page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>",
                Encoding.UTF8,
                "text/html")
        };
        var sut = _mocker.CreateInstance<AmazonPrimePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("Amazon Prime Video");
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "Prime Video page extract populates ShowName from structured series metadata when distinct from the episode title, " +
        "so GET submit lookup can return podcastName for unknown Prime URLs.")]
    public async Task extracts_series_name_for_lookup()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var seriesName = _fixture.CreateTitle();
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"<html><head>" +
                $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
                $"<meta property=\"og:series\" content=\"{seriesName}\" />" +
                $"</head></html>",
                Encoding.UTF8,
                "text/html")
        };
        var sut = _mocker.CreateInstance<AmazonPrimePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Amazon Prime Video");
    }

    [Fact(DisplayName =
        "Prime Video page extract recovers title and ShowName from ATV embedded JSON when og:title is missing, " +
        "because live season pages often expose only parentTitle plus a document title.")]
    public async Task extracts_series_from_atv_json_when_og_title_missing()
    {
        // Arrange
        var seriesName = _fixture.CreateTitle();
        var seasonLabel = $"{seriesName} - Season 1";
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"<html><head><title>Prime Video: {seasonLabel}</title></head>" +
                $"<body><script>window.__ATV={{\"titleType\":\"season\",\"entityType\":\"TV Show\"," +
                $"\"parentTitle\":\"{seriesName}\"}};</script></body></html>",
                Encoding.UTF8,
                "text/html")
        };
        var sut = _mocker.CreateInstance<AmazonPrimePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seasonLabel);
        meta.ShowName.Should().Be(seriesName);
        meta.Publisher.Should().Be("Amazon Prime Video");
    }

    [Fact(DisplayName =
        "Prime Video movie pages leave ShowName null even when ATV JSON is present, " +
        "because a film has no parent series for podcastName attach.")]
    public async Task movie_pages_do_not_set_show_name()
    {
        // Arrange
        var filmTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"<html><head><title>Prime Video: {filmTitle}</title></head>" +
                $"<body><script>window.__ATV={{\"titleType\":\"movie\",\"entityType\":\"Movie\"}};</script></body></html>",
                Encoding.UTF8,
                "text/html")
        };
        var sut = _mocker.CreateInstance<AmazonPrimePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(filmTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Prime Video season pages prefer parentTitle co-located with titleType/entityType season markers, " +
        "so an earlier carousel parentTitle in the HTML cannot become podcastName.")]
    public async Task prefers_season_parent_title_over_earlier_carousel_parent()
    {
        // Arrange
        var carouselNoise = _fixture.CreateTitle();
        var seriesName = _fixture.CreateTitle();
        var seasonLabel = $"{seriesName} - Season 1";
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"<html><head><title>Prime Video: {seasonLabel}</title></head>" +
                $"<body><script>window.__RELATED={{\"parentTitle\":\"{carouselNoise}\"}};</script>" +
                $"<script>window.__ATV={{\"titleType\":\"season\",\"entityType\":\"TV Show\"," +
                $"\"parentTitle\":\"{seriesName}\"}};</script></body></html>",
                Encoding.UTF8,
                "text/html")
        };
        var sut = _mocker.CreateInstance<AmazonPrimePageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(seasonLabel);
        meta.ShowName.Should().Be(seriesName);
        meta.ShowName.Should().NotBe(carouselNoise);
    }

    [Fact(DisplayName =
        "Prime Video page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<AmazonPrimePageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "AddAmazonPrimeServices registers a catalog-keyed adapter for Prime Video URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_amazon_prime_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAmazonPrimeServices();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.AmazonPrime);
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
