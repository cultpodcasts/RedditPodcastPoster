using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.BBC.Tests.BusinessRules;

/// <summary>
/// iPlayer shows the broadcast time as UK wall-clock time; the extractor stores the UTC instant.
/// Fixed months (July for BST, January for GMT) are deliberate: the rule is about the UK daylight-saving offset,
/// so the date must fall on a known side of the clock change. The year is relative to today.
/// </summary>
public class iPlayerPageMetaDataExtractorReleaseRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Theory(DisplayName =
        "When an iPlayer page shows a UK broadcast time, then the release is that time converted to UTC (an hour earlier in BST, unchanged in GMT), because the API stores and emits UTC instants.")]
    [InlineData(7, 1)]
    [InlineData(1, 0)]
    public async Task uk_broadcast_time_is_stored_as_utc(int month, int ukOffsetHours)
    {
        // Arrange
        var day = new DateTime(DomainTestFixture.UtcToday.Year + 1, month, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var url = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");
        var html = BuildIplayerHtml(_fixture.CreateTitle(), _fixture.CreateTitle(),
            $"9pm {day.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };
        var sut = _mocker.CreateInstance<iPlayerPageMetaDataExtractor>();

        // Act
        var meta = await sut.Extract(url, response);

        // Assert
        meta.Release.Should().Be(DateTime.SpecifyKind(day.AddHours(21 - ukOffsetHours), DateTimeKind.Utc));
        meta.Release!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static string BuildIplayerHtml(string title, string description, string release) =>
        $"""
         <html>
         <head>
         <meta property="og:type" content="video.episode"/>
         <meta property="og:title" content="{title}"/>
         <meta property="og:description" content="{description}"/>
         </head>
         <body>
         <span class="episode-metadata__text">1h 30m</span>
         <span class="episode-metadata__text">{release}</span>
         </body>
         </html>
         """;
}
