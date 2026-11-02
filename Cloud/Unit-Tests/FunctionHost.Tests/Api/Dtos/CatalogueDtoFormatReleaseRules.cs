using System.Globalization;
using System.Text.Json;
using Api.Dtos.Extensions;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

/// <summary>#1220: curator GET <c>release</c> keeps year / date / datetime precision on the wire.</summary>
public class CatalogueDtoFormatReleaseRules
{
    private static readonly JsonSerializerOptions ApiOptions = new(JsonSerializerDefaults.Web);
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "FormatRelease: when the catalogue release is null, the wire value is null, because omitted releases stay omitted.")]
    public void format_release_null_is_null()
    {
        // Arrange
        CatalogueRelease? release = null;

        // Act
        var wire = CatalogueDtoMapping.FormatRelease(release);

        // Assert
        wire.Should().BeNull();
    }

    [Fact(DisplayName =
        "FormatRelease: when precision is Year, the wire value is yyyy, because year-only film releases have no month or day.")]
    public void format_release_year_is_yyyy()
    {
        // Arrange
        var year = DomainTestFixture.UtcToday.Year - 15;
        var release = CatalogueRelease.FromYear(year);

        // Act
        var wire = CatalogueDtoMapping.FormatRelease(release);

        // Assert
        wire.Should().Be(year.ToString("D4", CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName =
        "FormatRelease: when precision is Date, the wire value is yyyy-MM-dd, because date-only releases have no time of day.")]
    public void format_release_date_is_yyyy_mm_dd()
    {
        // Arrange
        var day = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(11));
        var release = CatalogueRelease.FromDate(day);

        // Act
        var wire = CatalogueDtoMapping.FormatRelease(release);

        // Assert
        wire.Should().Be(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName =
        "FormatRelease: when precision is DateTimeUtc, the wire value is yyyy-MM-ddTHH:mm:ssZ, because API datetimes are UTC ISO-8601 with a Z and must keep the time of day.")]
    public void format_release_datetime_is_utc_z()
    {
        // Arrange
        var instant = DomainTestFixture.UtcAtTime(-7, _fixture.CreateNonMidnightTimeOfDay());
        var release = CatalogueRelease.FromDateTimeUtc(instant);

        // Act
        var wire = CatalogueDtoMapping.FormatRelease(release);

        // Assert
        wire.Should().Be(instant.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture));
        wire.Should().Contain("T");
        wire.Should().EndWith("Z");
        wire.Should().NotBe(DateOnly.FromDateTime(instant).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName =
        "FilmDto JSON: when the film release is DateTimeUtc, the release property is yyyy-MM-ddTHH:mm:ssZ, because the curator GET must not drop time of day.")]
    public void film_dto_json_release_keeps_datetime()
    {
        // Arrange
        var instant = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        film.SetRelease(CatalogueRelease.FromDateTimeUtc(instant));
        var expected = instant.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture);

        // Act
        var json = JsonSerializer.Serialize(film.ToDto(), ApiOptions);

        // Assert
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("release").GetString().Should().Be(expected);
    }

    [Fact(DisplayName =
        "Playable DTO JSON: when a TV episode or news report release is DateTimeUtc, the release property is yyyy-MM-ddTHH:mm:ssZ on both, because WithPlayableFields shares FormatRelease.")]
    public void playable_dto_json_release_keeps_datetime()
    {
        // Arrange
        var instant = DomainTestFixture.UtcAtTime(-9, _fixture.CreateNonMidnightTimeOfDay());
        var release = CatalogueRelease.FromDateTimeUtc(instant);
        var expected = instant.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture);
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        episode.SetRelease(release);
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        report.SetRelease(release);

        // Act
        var episodeJson = JsonSerializer.Serialize(episode.ToDto(), ApiOptions);
        var reportJson = JsonSerializer.Serialize(report.ToDto(), ApiOptions);

        // Assert
        using var episodeDoc = JsonDocument.Parse(episodeJson);
        using var reportDoc = JsonDocument.Parse(reportJson);
        episodeDoc.RootElement.GetProperty("release").GetString().Should().Be(expected);
        reportDoc.RootElement.GetProperty("release").GetString().Should().Be(expected);
    }

    [Fact(DisplayName =
        "FilmDto JSON: when the film release is Year or Date, the release property stays yyyy or yyyy-MM-dd, because coarser precision must not invent a time of day.")]
    public void film_dto_json_release_keeps_year_and_date()
    {
        // Arrange
        var year = DomainTestFixture.UtcToday.Year - 12;
        var day = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(21));
        var yearFilm = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        yearFilm.SetRelease(CatalogueRelease.FromYear(year));
        var dateFilm = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        dateFilm.SetRelease(CatalogueRelease.FromDate(day));

        // Act
        var yearJson = JsonSerializer.Serialize(yearFilm.ToDto(), ApiOptions);
        var dateJson = JsonSerializer.Serialize(dateFilm.ToDto(), ApiOptions);

        // Assert
        using var yearDoc = JsonDocument.Parse(yearJson);
        using var dateDoc = JsonDocument.Parse(dateJson);
        yearDoc.RootElement.GetProperty("release").GetString()
            .Should().Be(year.ToString("D4", CultureInfo.InvariantCulture));
        dateDoc.RootElement.GetProperty("release").GetString()
            .Should().Be(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
