using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.CbcGem.Extensions;
using RedditPodcastPoster.CbcGem.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.CbcGem.Tests.BusinessRules;

/// <summary>
/// Pages are synthetic: the shape mirrors CBC Gem's server-rendered head (one JSON-LD block,
/// og:type video.tv_show on every page, a generic social og:image), values come from fixtures.
/// </summary>
public class CbcGemPageMetaDataExtractorRules
{
    private const string GenericSocialImage =
        "https://images.example.test/v1/cbc-gem/16x9/cbc-gem-social-media.jpg";

    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public CbcGemPageMetaDataExtractorRules()
    {
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "A CBC Gem page whose JSON-LD is a Movie is a film: MadeAsFilm is set, ShowName is unset, and title, " +
        "abstract, duration, UTC release and sized image come from JSON-LD even though og:type says tv_show, " +
        "so submit stores a Film rather than a series episode.")]
    public async Task movie_json_ld_is_a_film()
    {
        // Arrange
        var slug = CreateSlug();
        var title = _fixture.CreateTitle();
        var description = _fixture.CreateShortTitle();
        var minutes = 1 + Math.Abs(_fixture.Create<int>() % 150);
        var release = DomainTestFixture.UtcAtTime(-7, _fixture.CreateNonMidnightTimeOfDay());
        var imageBase = $"https://images.example.test/v1/{slug}.jpg?impolicy=ott&im=Resize=";
        _handler.Response = OkHtml(Page(JsonLd("Movie",
            ("name", title),
            ("abstract", description),
            ("duration", $"PT0H{minutes}M"),
            ("datePublished", release.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture)),
            ("image", imageBase + "(_Size_)")), ogTitle: $"{title} | Movies | CBC Gem"));
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(new Uri($"https://gem.cbc.ca/{slug}?autoplay=1"));

        // Assert
        meta.MadeAsFilm.Should().BeTrue();
        meta.ShowName.Should().BeNull();
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Duration.Should().Be(TimeSpan.FromMinutes(minutes));
        meta.Release.Should().Be(release);
        meta.Release!.Value.Kind.Should().Be(DateTimeKind.Utc);
        meta.Image.Should().Be(new Uri(imageBase + "1200"));
        meta.Publisher.Should().Be("CBC Gem");
        _handler.LastRequestUri.Should().Be(new Uri($"https://gem.cbc.ca/{slug}"));
    }

    [Fact(DisplayName =
        "A CBC Gem page whose JSON-LD is a TVEpisode is a series episode: ShowName comes from partofSeries " +
        "(CBC Gem's spelling) and MadeAsFilm stays false, so an episode is never filed as a film.")]
    public async Task tv_episode_json_ld_is_a_series_episode()
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var title = _fixture.CreateTitle();
        var html = Page(JsonLd("TVEpisode",
            ("name", title),
            ("partofSeries", JsonLd("CreativeWorkSeries", ("name", series)))), ogTitle: $"{title} | {series} | CBC Gem");
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}/s01e02"), html);

        // Assert
        meta.Title.Should().Be(title);
        meta.ShowName.Should().Be(series);
        meta.MadeAsFilm.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A CBC Gem series hub (JSON-LD TVSeries) fails extract, " +
        "because a whole series is neither a film nor one episode and must not be stored as either.")]
    public async Task tv_series_hub_fails_extract()
    {
        // Arrange
        var html = Page(JsonLd("TVSeries", ("name", _fixture.CreateTitle())), ogTitle: null);
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>()
            .WithMessage("*series page*");
    }

    [Fact(DisplayName =
        "A CBC Gem page without Movie or TVEpisode JSON-LD fails extract even when og:title is present, " +
        "because og:type is tv_show on every Gem page and cannot tell a film from anything else.")]
    public async Task page_without_catalogue_json_ld_fails_extract()
    {
        // Arrange
        var html = $"<html><head><meta property=\"og:title\" content=\"{_fixture.CreateTitle()} | CBC Gem\"/>" +
                   "<meta property=\"og:type\" content=\"video.tv_show\"/></head></html>";
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "CBC Gem page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(new Uri($"https://gem.cbc.ca/{CreateSlug()}"));

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "A CBC Gem datePublished with a UTC offset converts to UTC, " +
        "because API datetimes are UTC.")]
    public async Task offset_release_converts_to_utc()
    {
        // Arrange
        var release = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var local = new DateTimeOffset(release).ToOffset(TimeSpan.FromHours(-5));
        var html = Page(JsonLd("Movie",
            ("name", _fixture.CreateTitle()),
            ("datePublished", local.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture))), ogTitle: null);
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        meta.Release.Should().Be(release);
        meta.Release!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName =
        "A date-only CBC Gem datePublished stays at midnight on that date, unshifted, " +
        "because a release with no time must not move to another day.")]
    public async Task date_only_release_stays_midnight()
    {
        // Arrange
        var date = DomainTestFixture.UtcDateDaysAgo(10);
        var html = Page(JsonLd("Movie",
            ("name", _fixture.CreateTitle()),
            ("datePublished", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))), ogTitle: null);
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        meta.Release.Should().Be(date);
        meta.Release!.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName =
        "The generic CBC Gem social og:image is not used as artwork when JSON-LD has no image, " +
        "because it is the same platform card on every page.")]
    public async Task generic_social_image_is_ignored()
    {
        // Arrange
        var html = Page(JsonLd("Movie", ("name", _fixture.CreateTitle())), ogTitle: null);
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        meta.Image.Should().BeNull();
    }

    [Fact(DisplayName =
        "AddCbcGemServices registers a catalog-keyed CBC Gem adapter that stores the bare canonical URL " +
        "and extracts from Worker-supplied HTML without an HTTP GET.")]
    public async Task add_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCbcGemServices();
        using var provider = services.BuildServiceProvider();
        var slug = CreateSlug();
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://gem.cbc.ca/{slug}?autoplay=1");
        var html = Page(JsonLd("Movie", ("name", title)), ogTitle: null);

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));
        var meta = await adapter.ExtractMetaData(url, html);

        // Assert
        adapter.ResolveService(url).Should().Be(StreamingService.CbcGem);
        adapter.CanonicalStoredUrl(url).Should().Be(new Uri($"https://gem.cbc.ca/{slug}"));
        meta.Title.Should().Be(title);
        meta.MadeAsFilm.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A CBC Gem TVEpisode without partofSeries takes its ShowName from og:title '{title} | {series} | CBC Gem', " +
        "so an episode is still filed under its show when JSON-LD omits the series.")]
    public async Task episode_series_falls_back_to_og_title()
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var title = _fixture.CreateTitle();
        var html = Page(JsonLd("TVEpisode", ("name", title)), ogTitle: $"{title} | {series} | CBC Gem");
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}/s01e03"), html);

        // Assert
        meta.ShowName.Should().Be(series);
        meta.MadeAsFilm.Should().BeFalse();
    }

    [Theory(DisplayName =
        "A CBC Gem TVEpisode with no series anywhere, or whose only series is the publisher 'CBC Gem', fails extract, " +
        "because an episode cannot be filed under a show it does not name.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task episode_without_real_series_fails_extract(bool publisherAsSeries)
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var node = publisherAsSeries
            ? JsonLd("TVEpisode", ("name", title), ("partofSeries", JsonLd("CreativeWorkSeries", ("name", "CBC Gem"))))
            : JsonLd("TVEpisode", ("name", title));
        var html = Page(node, ogTitle: $"{title} | CBC Gem");
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var act = async () =>
            await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}/s01e01"), html);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>()
            .WithMessage("*no series name*");
    }

    [Theory(DisplayName =
        "A CBC Gem TVEpisode with no image of its own uses partofSeason's image, then partofSeries's, sized to 1200, " +
        "because live episode pages carry artwork on the season or series rather than the episode.")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task episode_image_falls_back_to_season_then_series(bool seasonHasImage)
    {
        // Arrange
        var slug = CreateSlug();
        var seasonImage = $"https://images.example.test/v1/{slug}-season.jpg?im=Resize=(_Size_)";
        var seriesImage = $"https://images.example.test/v1/{slug}-series.jpg?im=Resize=(_Size_)";
        var season = seasonHasImage
            ? JsonLd("TVSeason", ("name", _fixture.CreateShortTitle()), ("image", seasonImage))
            : JsonLd("TVSeason", ("name", _fixture.CreateShortTitle()));
        var html = Page(JsonLd("TVEpisode",
            ("name", _fixture.CreateTitle()),
            ("partofSeason", season),
            ("partofSeries", JsonLd("CreativeWorkSeries", ("name", _fixture.CreateTitle()), ("image", seriesImage)))),
            ogTitle: null);
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{slug}/s01e01"), html);

        // Assert
        var expected = (seasonHasImage ? seasonImage : seriesImage).Replace("(_Size_)", "1200");
        meta.Image.Should().Be(new Uri(expected));
    }

    [Theory(DisplayName =
        "CBC Gem JSON-LD wrapped in an array or an @graph is still read, " +
        "so a change in how the block is wrapped does not break extract.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task wrapped_json_ld_is_read(bool useGraph)
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var organisation = JsonLd("Organization", ("name", _fixture.CreateShortTitle()));
        var movie = JsonLd("Movie", ("name", title));
        object wrapped = useGraph
            ? new Dictionary<string, object> { ["@context"] = "https://schema.org", ["@graph"] = new object[] { organisation, movie } }
            : new object[] { organisation, movie };
        var html = Page(wrapped, ogTitle: null);
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        meta.Title.Should().Be(title);
        meta.MadeAsFilm.Should().BeTrue();
    }

    [Theory(DisplayName =
        "A TVSeries node ahead of the item does not turn a CBC Gem episode or film into a rejected series hub: " +
        "the node matching the URL shape wins, so extra series JSON-LD never blocks a valid submit.")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task series_node_before_item_does_not_reject(bool episodeUrl)
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var title = _fixture.CreateTitle();
        var item = episodeUrl
            ? JsonLd("TVEpisode", ("name", title), ("partofSeries", JsonLd("CreativeWorkSeries", ("name", series))))
            : JsonLd("Movie", ("name", title));
        var html = Pages(null, JsonLd("TVSeries", ("name", series)), item);
        var slug = CreateSlug();
        var url = new Uri(episodeUrl ? $"https://gem.cbc.ca/{slug}/s02e04" : $"https://gem.cbc.ca/{slug}");
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(url, html);

        // Assert
        meta.Title.Should().Be(title);
        meta.MadeAsFilm.Should().Be(!episodeUrl);
    }

    [Fact(DisplayName =
        "A CBC Gem slug URL whose JSON-LD has a TVSeries plus a TVEpisode (a show hub listing a featured episode) " +
        "fails extract as a series page, because the /{slug} URL is the hub and must not be filed as that episode.")]
    public async Task slug_url_with_series_and_episode_is_rejected_as_series()
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var html = Pages(null,
            JsonLd("TVSeries", ("name", series)),
            JsonLd("TVEpisode", ("name", _fixture.CreateTitle()),
                ("partofSeries", JsonLd("CreativeWorkSeries", ("name", series)))));
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var act = async () => await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>()
            .WithMessage("*series page*");
    }

    [Fact(DisplayName =
        "A CBC Gem slug URL whose JSON-LD has a TVSeries ahead of a Movie is read as the Movie, " +
        "because a film page is never rejected for carrying related series JSON-LD.")]
    public async Task slug_url_with_series_and_movie_gives_movie()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var html = Pages(null,
            JsonLd("TVSeries", ("name", _fixture.CreateTitle())),
            JsonLd("Movie", ("name", title)));
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        meta.Title.Should().Be(title);
        meta.MadeAsFilm.Should().BeTrue();
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "An episode URL whose page has both Movie and TVEpisode JSON-LD is read as the TVEpisode, " +
        "because the /{slug}/sNNeNN URL shape says it is an episode.")]
    public async Task episode_url_prefers_tv_episode_over_movie()
    {
        // Arrange
        var series = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var html = Pages(null,
            JsonLd("Movie", ("name", _fixture.CreateTitle())),
            JsonLd("TVEpisode", ("name", episodeTitle), ("partofSeries", JsonLd("CreativeWorkSeries", ("name", series)))));
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}/s01e01"), html);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(series);
        meta.MadeAsFilm.Should().BeFalse();
    }

    [Theory(DisplayName =
        "A CBC Gem description falls back from JSON-LD abstract to JSON-LD description, then og:description, " +
        "so a page missing abstract still gets a description.")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task description_falls_back_when_abstract_missing(bool jsonLdDescription)
    {
        // Arrange
        var description = _fixture.CreateShortTitle();
        var node = jsonLdDescription
            ? JsonLd("Movie", ("name", _fixture.CreateTitle()), ("description", description))
            : JsonLd("Movie", ("name", _fixture.CreateTitle()));
        var ogDescription = jsonLdDescription ? null : description;
        var html = Pages(ogDescription, node);
        var sut = _mocker.CreateInstance<CbcGemPageMetaDataExtractor>();

        // Act
        var meta = await sut.ExtractFromHtml(new Uri($"https://gem.cbc.ca/{CreateSlug()}"), html);

        // Assert
        meta.Description.Should().Be(description);
    }

    private string CreateSlug() =>
        Regex.Replace(_fixture.CreateTitle().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static Dictionary<string, object> JsonLd(string type, params (string Key, object Value)[] properties)
    {
        var node = new Dictionary<string, object> { ["@type"] = type };
        foreach (var (key, value) in properties)
        {
            node[key] = value;
        }

        return node;
    }

    private static string Page(object jsonLd, string? ogTitle)
    {
        var json = JsonSerializer.Serialize(jsonLd);
        var og = ogTitle is null
            ? string.Empty
            : $"<meta property=\"og:title\" content=\"{WebUtility.HtmlEncode(ogTitle)}\"/>";
        return "<html><head>" + og +
               "<meta property=\"og:type\" content=\"video.tv_show\"/>" +
               $"<meta property=\"og:image\" content=\"{GenericSocialImage}\"/>" +
               $"<script type=\"application/ld+json\">{json}</script>" +
               "</head><body></body></html>";
    }

    private static string Pages(string? ogDescription, params object[] jsonLdBlocks)
    {
        var og = ogDescription is null
            ? string.Empty
            : $"<meta property=\"og:description\" content=\"{WebUtility.HtmlEncode(ogDescription)}\"/>";
        var blocks = string.Concat(jsonLdBlocks.Select(block =>
            $"<script type=\"application/ld+json\">{JsonSerializer.Serialize(block)}</script>"));
        return "<html><head>" + og +
               "<meta property=\"og:type\" content=\"video.tv_show\"/>" +
               $"<meta property=\"og:image\" content=\"{GenericSocialImage}\"/>" +
               blocks + "</head><body></body></html>";
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
