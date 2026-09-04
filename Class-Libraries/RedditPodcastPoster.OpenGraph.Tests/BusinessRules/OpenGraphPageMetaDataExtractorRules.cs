using System.Net;
using System.Text;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.OpenGraph.Tests.BusinessRules;

public class OpenGraphPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly OpenGraphPageMetaDataExtractor _sut = new();

    [Fact(DisplayName =
        "Open Graph extract reads title, description, image, ISO duration, and datePublished from the page, " +
        "so Netflix and Prime submit can fill an episode without a podcast catalogue API.")]
    public async Task extracts_open_graph_and_json_ld_fields()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var image = new Uri($"https://example.test/art/{_fixture.CreateYouTubeId()}");
        var publisher = _fixture.Create<string>();
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{title}\" />" +
            $"<meta property=\"og:description\" content=\"{description}\" />" +
            $"<meta property=\"og:image\" content=\"{image}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"VideoObject\",\"duration\":\"PT1H2M3S\",\"datePublished\":\"2024-03-15T18:00:00Z\"}}" +
            $"</script></head></html>";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        // Act
        var meta = await _sut.Extract(url, response, publisher);

        // Assert
        meta.Title.Should().Be(title);
        meta.Description.Should().Be(description);
        meta.Image.Should().Be(image);
        meta.Publisher.Should().Be(publisher);
        meta.Duration.Should().Be(new TimeSpan(1, 2, 3));
        meta.Release.Should().Be(DateTime.Parse("2024-03-15T18:00:00Z").ToUniversalTime());
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Open Graph extract sets ShowName from og:video:series when it differs from og:title, " +
        "so Netflix/Prime lookup can return podcastName without treating the platform publisher as the series.")]
    public async Task og_video_series_is_show_name_when_distinct_from_title()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var seriesName = _fixture.CreateTitle();
        var publisher = _fixture.Create<string>();
        var url = new Uri($"https://www.netflix.com/watch/{_fixture.CreateAppleId()}");
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<meta property=\"og:video:series\" content=\"{seriesName}\" />" +
            $"</head></html>";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        // Act
        var meta = await _sut.Extract(url, response, publisher);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.ShowName.Should().NotBe(meta.Title);
        meta.ShowName.Should().NotBe(publisher);
    }

    [Fact(DisplayName =
        "Open Graph extract sets ShowName from JSON-LD partOfSeries when it differs from og:title, " +
        "because episode pages often omit og:video:series but still expose structured series metadata.")]
    public async Task json_ld_part_of_series_is_show_name_when_distinct_from_title()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var seriesName = _fixture.CreateTitle();
        var publisher = _fixture.Create<string>();
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVEpisode\",\"name\":\"{episodeTitle}\"," +
            $"\"partOfSeries\":{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}}}" +
            $"</script></head></html>";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        // Act
        var meta = await _sut.Extract(url, response, publisher);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(seriesName);
    }

    [Fact(DisplayName =
        "Open Graph extract sets ShowName from JSON-LD TVSeries.name on catalogue pages, " +
        "so Netflix series title pages return podcastName for attach-by-name without og:video:series.")]
    public async Task json_ld_tv_series_name_is_show_name_on_catalogue_pages()
    {
        // Arrange
        var marketingTitle = $"Watch {_fixture.CreateTitle()} | Netflix Official Site";
        var seriesName = _fixture.CreateTitle();
        var publisher = "Netflix";
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{marketingTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"TVSeries\",\"name\":\"{seriesName}\"}}" +
            $"</script></head></html>";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        // Act
        var meta = await _sut.Extract(url, response, publisher);

        // Assert
        meta.Title.Should().Be(marketingTitle);
        meta.ShowName.Should().Be(seriesName);
        meta.ShowName.Should().NotBe(publisher);
    }

    [Fact(DisplayName =
        "Open Graph extract does not set ShowName from JSON-LD Movie.name, " +
        "because a film has no parent series and podcastName must stay null.")]
    public async Task json_ld_movie_name_is_not_show_name()
    {
        // Arrange
        var marketingTitle = $"Watch {_fixture.CreateTitle()} | Netflix Official Site";
        var movieName = _fixture.CreateTitle();
        var publisher = "Netflix";
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{marketingTitle}\" />" +
            $"<script type=\"application/ld+json\">" +
            $"{{\"@type\":\"Movie\",\"name\":\"{movieName}\"}}" +
            $"</script></head></html>";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        // Act
        var meta = await _sut.Extract(url, response, publisher);

        // Assert
        meta.Title.Should().Be(marketingTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Open Graph extract does not set ShowName to og:title alone, because og:title on watch pages is the episode title, not the series.")]
    public async Task og_title_alone_is_not_show_name()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var publisher = "Netflix";
        var url = new Uri($"https://www.netflix.com/watch/{_fixture.CreateAppleId()}");
        var html =
            $"<html><head><meta property=\"og:title\" content=\"{episodeTitle}\" /></head></html>";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        // Act
        var meta = await _sut.Extract(url, response, publisher);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Open Graph extract ignores a series candidate that equals the platform publisher, " +
        "so podcastName is never the Netflix or Prime brand.")]
    public async Task platform_publisher_is_not_show_name()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var publisher = "Netflix";
        var url = new Uri($"https://www.netflix.com/watch/{_fixture.CreateAppleId()}");
        var html =
            $"<html><head>" +
            $"<meta property=\"og:title\" content=\"{episodeTitle}\" />" +
            $"<meta property=\"og:video:series\" content=\"{publisher}\" />" +
            $"</head></html>";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        // Act
        var meta = await _sut.Extract(url, response, publisher);

        // Assert
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Open Graph extract reads og:title from a name attribute when property is absent, " +
        "because Channel 4 SSR emits name=\"og:title\" rather than property=\"og:title\".")]
    public async Task name_attribute_og_title_is_accepted()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var publisher = _fixture.Create<string>();
        var url = new Uri($"https://www.channel4.com/programmes/{_fixture.CreateYouTubeId()}");
        var html =
            $"<html><head><meta name=\"og:title\" content=\"{title}\" /></head></html>";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

        // Act
        var meta = await _sut.Extract(url, response, publisher);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be(publisher);
    }

    [Fact(DisplayName =
        "Open Graph extract fails when og:title is missing, because an episode cannot be created without a title.")]
    public async Task missing_title_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><head></head></html>", Encoding.UTF8, "text/html")
        };

        // Act
        var act = async () => await _sut.Extract(url, response, _fixture.Create<string>());

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }
}
