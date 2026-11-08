using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

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

    [Theory(DisplayName =
        "When an iPlayer page shows a 12-hour broadcast time, then 12am is midnight and 12pm is midday, because the hour feeds the UTC conversion and a midday broadcast must not land on the next day.")]
    [InlineData("12am", 0, 0)]
    [InlineData("12pm", 12, 0)]
    [InlineData("12:30pm", 12, 30)]
    [InlineData("12:15am", 0, 15)]
    [InlineData("1am", 1, 0)]
    [InlineData("11pm", 23, 0)]
    public async Task twelve_hour_clock_is_converted_correctly(string time, int hour24, int minute)
    {
        // Arrange
        var day = new DateTime(DomainTestFixture.UtcToday.Year + 1, 1, 15, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var meta = await ExtractRelease($"{time} {day.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");

        // Assert
        meta.Release.Should().Be(DateTime.SpecifyKind(day.AddHours(hour24).AddMinutes(minute), DateTimeKind.Utc));
    }

    [Theory(DisplayName =
        "When an iPlayer broadcast time falls in the UK clock-change hour, then a March gap time is read as GMT and a repeated October time is the second (GMT) occurrence, because the conversion must be deterministic at both daylight-saving edges.")]
    [InlineData(3)]
    [InlineData(10)]
    public async Task clock_change_hour_is_read_as_gmt(int month)
    {
        // Arrange
        var year = DomainTestFixture.UtcToday.Year + 1;
        var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Unspecified);
        var changeDay = lastDay.AddDays(-(int)lastDay.DayOfWeek);

        // Act
        var meta = await ExtractRelease($"1:30am {changeDay.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");

        // Assert
        meta.Release.Should().Be(DateTime.SpecifyKind(changeDay.AddHours(1).AddMinutes(30), DateTimeKind.Utc));
        meta.Release!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName =
        "When an iPlayer page shows only a date, then the release is that date at midnight with no BST shift, because a calendar date is not a UK instant.")]
    public async Task date_only_release_is_not_shifted()
    {
        // Arrange
        var day = new DateTime(DomainTestFixture.UtcToday.Year + 1, 7, 15, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var meta = await ExtractRelease(day.ToString("d MMM yyyy", CultureInfo.InvariantCulture));

        // Assert
        meta.Release.Should().Be(day);
        meta.Release!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    private async Task<NonPodcastServiceItemMetaData> ExtractRelease(string release)
    {
        var url = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");
        var html = BuildIplayerHtml(_fixture.CreateTitle(), _fixture.CreateTitle(), release);
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };
        var sut = _mocker.CreateInstance<iPlayerPageMetaDataExtractor>();
        return await sut.Extract(url, response);
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
