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
