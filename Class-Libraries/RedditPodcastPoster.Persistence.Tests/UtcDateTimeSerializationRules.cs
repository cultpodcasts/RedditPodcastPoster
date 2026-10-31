using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Serialization;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// Every DateTime / DateTimeOffset that goes through the shared serializer options (Cosmos documents, Cosmos LINQ
/// constants, file backups) is UTC with a trailing Z; text with no zone is read as UTC and text with an offset is
/// adjusted to UTC.
/// </summary>
public class UtcDateTimeSerializationRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly JsonSerializerOptions _options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    private DateTime CreateUtcInstant() =>
        DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 300 + 1),
            TimeSpan.FromMinutes(_fixture.Create<int>() % 1439 + 1));

    [Fact(DisplayName =
        "When a UTC DateTime is written and read back, then the text ends in Z and the instant and UTC kind survive, because UTC values already stored must keep the exact same text.")]
    public void utc_date_time_round_trips_unchanged()
    {
        // Arrange
        var value = CreateUtcInstant();
        var defaultText = JsonSerializer.Serialize(value);

        // Act
        var json = JsonSerializer.Serialize(value, _options);
        var read = JsonSerializer.Deserialize<DateTime>(json, _options);

        // Assert
        json.Should().Be(defaultText).And.EndWith("Z\"");
        read.Should().Be(value);
        read.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName =
        "When a stored date-time has no zone, then it is read as that UTC instant and written back with a Z, because every stored date-time is UTC and a zone-less value must not be read as server-local time.")]
    public void zone_less_text_is_read_as_utc()
    {
        // Arrange
        var value = CreateUtcInstant();
        var zoneLess = $"\"{value:yyyy-MM-ddTHH:mm:ss}\"";

        // Act
        var read = JsonSerializer.Deserialize<DateTime>(zoneLess, _options);
        var written = JsonSerializer.Serialize(read, _options);

        // Assert
        read.Should().Be(value);
        read.Kind.Should().Be(DateTimeKind.Utc);
        written.Should().Be($"\"{value:yyyy-MM-ddTHH:mm:ss}Z\"");
    }

    [Fact(DisplayName =
        "When a date-time carries a UTC offset, then it is read as the same instant in UTC and written back with a Z, because the API never emits an offset.")]
    public void offset_text_is_adjusted_to_utc()
    {
        // Arrange
        var value = CreateUtcInstant();
        var offset = TimeSpan.FromHours(_fixture.Create<int>() % 12 + 1);
        var withOffset = JsonSerializer.Serialize(new DateTimeOffset(value).ToOffset(offset));

        // Act
        var read = JsonSerializer.Deserialize<DateTime>(withOffset, _options);
        var readOffset = JsonSerializer.Deserialize<DateTimeOffset>(withOffset, _options);

        // Assert
        withOffset.Should().NotEndWith("Z\"");
        read.Should().Be(value);
        read.Kind.Should().Be(DateTimeKind.Utc);
        JsonSerializer.Serialize(read, _options).Should().Be($"\"{value:yyyy-MM-ddTHH:mm:ss}Z\"");
        readOffset.Offset.Should().Be(TimeSpan.Zero);
        readOffset.UtcDateTime.Should().Be(value);
    }

    [Fact(DisplayName =
        "When an unspecified-kind DateTime is written, then it is written as that UTC instant with a Z, because a parsed date with no zone is a UTC value.")]
    public void unspecified_kind_is_written_as_utc()
    {
        // Arrange
        var value = CreateUtcInstant();
        var unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        // Act
        var json = JsonSerializer.Serialize(unspecified, _options);

        // Assert
        json.Should().Be($"\"{value:yyyy-MM-ddTHH:mm:ss}Z\"");
    }

    [Fact(DisplayName =
        "When a local-kind DateTime is written, then it is converted to the same instant in UTC with a Z, because the server's time zone must never leak into stored or emitted date-times.")]
    public void local_kind_is_converted_to_utc()
    {
        // Arrange
        var value = CreateUtcInstant();
        var local = value.ToLocalTime();

        // Act
        var json = JsonSerializer.Serialize(local, _options);

        // Assert
        json.Should().Be($"\"{value:yyyy-MM-ddTHH:mm:ss}Z\"");
    }

    [Fact(DisplayName =
        "When a DateTimeOffset with a non-zero offset is written, then it is written as the UTC instant with a Z, because the API always emits UTC.")]
    public void date_time_offset_is_written_as_utc()
    {
        // Arrange
        var value = CreateUtcInstant();
        var withOffset = new DateTimeOffset(value).ToOffset(TimeSpan.FromHours(_fixture.Create<int>() % 12 + 1));

        // Act
        var json = JsonSerializer.Serialize(withOffset, _options);

        // Assert
        json.Should().Be($"\"{value:yyyy-MM-ddTHH:mm:ss}Z\"");
    }

    [Fact(DisplayName =
        "When nullable date-times are null they are still omitted, and when set they are written as UTC with a Z, because WhenWritingNull must keep stored documents free of explicit nulls.")]
    public void nullable_date_times_follow_ignore_condition_and_utc()
    {
        // Arrange
        var value = CreateUtcInstant();
        var empty = new NullableDates();
        var set = new NullableDates
        {
            At = DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            AtOffset = new DateTimeOffset(value).ToOffset(TimeSpan.FromHours(1))
        };

        // Act
        var emptyJson = JsonSerializer.Serialize(empty, _options);
        var setJson = JsonSerializer.Serialize(set, _options);
        var readBack = JsonSerializer.Deserialize<NullableDates>(setJson, _options)!;

        // Assert
        emptyJson.Should().Be("{}");
        setJson.Should().Be(
            $"{{\"at\":\"{value:yyyy-MM-ddTHH:mm:ss}Z\",\"atOffset\":\"{value:yyyy-MM-ddTHH:mm:ss}Z\"}}");
        readBack.At.Should().Be(value);
        readBack.At!.Value.Kind.Should().Be(DateTimeKind.Utc);
        JsonSerializer.Deserialize<NullableDates>("{\"at\":null,\"atOffset\":null}", _options)!.At.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a property opts into the local-offset converter, then its offset is kept and not flattened to Z, because the discovery schedule shows curators their own local time across daylight-saving changes.")]
    public void local_offset_property_keeps_its_offset()
    {
        // Arrange
        var value = CreateUtcInstant();
        var bst = new DateTimeOffset(value).ToOffset(TimeSpan.FromHours(1));
        var dto = new LocalOffsetHolder { Local = bst, Utc = bst };

        // Act
        var json = JsonSerializer.Serialize(dto, _options);
        var readBack = JsonSerializer.Deserialize<LocalOffsetHolder>(json, _options)!;

        // Assert
        json.Should().Be(
            $"{{\"local\":\"{bst:yyyy-MM-ddTHH:mm:ss}+01:00\",\"utc\":\"{value:yyyy-MM-ddTHH:mm:ss}Z\"}}");
        readBack.Local.Offset.Should().Be(TimeSpan.FromHours(1));
        readBack.Local.Should().Be(bst);
    }

    [Fact(DisplayName =
        "When only the DateTime and DateTimeOffset converters are registered, then nullable values still go through them and null stays null, because System.Text.Json wraps a registered converter for Nullable<T>.")]
    public void nullable_values_use_the_non_nullable_converters()
    {
        // Arrange
        var value = CreateUtcInstant();
        var options = new JsonSerializerOptions().AddUtcDateTimeConverters();
        DateTime? local = value.ToLocalTime();
        DateTimeOffset? withOffset = new DateTimeOffset(value).ToOffset(TimeSpan.FromHours(1));
        var expected = $"\"{value:yyyy-MM-ddTHH:mm:ss}Z\"";

        // Act
        var localJson = JsonSerializer.Serialize(local, options);
        var offsetJson = JsonSerializer.Serialize(withOffset, options);
        var nullJson = JsonSerializer.Serialize((DateTime?)null, options);
        var readZoneLess = JsonSerializer.Deserialize<DateTime?>($"\"{value:yyyy-MM-ddTHH:mm:ss}\"", options);
        var readNull = JsonSerializer.Deserialize<DateTimeOffset?>("null", options);

        // Assert
        options.Converters.Should().HaveCount(2);
        localJson.Should().Be(expected);
        offsetJson.Should().Be(expected);
        nullJson.Should().Be("null");
        readZoneLess.Should().Be(value);
        readZoneLess!.Value.Kind.Should().Be(DateTimeKind.Utc);
        readNull.Should().BeNull();
    }

    [Theory(DisplayName =
        "When a date-time field holds non-ISO text (culture-formatted, time-only or not a date) or a non-string token, then reading fails with a JsonException and TryParseIso rejects the text, because a guessed or clock-dependent instant must never be accepted.")]
    [InlineData("\"not-a-date\"")]
    [InlineData("\"10/11/2026\"")]
    [InlineData("\"Oct 31 2026\"")]
    [InlineData("\"31 October 2026 9pm\"")]
    [InlineData("\"12:30\"")]
    [InlineData("\"\"")]
    [InlineData("12345")]
    [InlineData("true")]
    public void non_iso_date_time_tokens_throw_json_exception(string json)
    {
        // Arrange
        var readDateTime = () => JsonSerializer.Deserialize<DateTime>(json, _options);
        var readDateTimeOffset = () => JsonSerializer.Deserialize<DateTimeOffset>(json, _options);
        var readNullable = () => JsonSerializer.Deserialize<DateTime?>(json, _options);
        var text = json.Trim('"');

        // Act
        var parsed = UtcDateTimes.TryParseIso(text, out _);

        // Assert
        readDateTime.Should().Throw<JsonException>();
        readDateTimeOffset.Should().Throw<JsonException>();
        readNullable.Should().Throw<JsonException>();
        parsed.Should().BeFalse();
    }

    [Theory(DisplayName =
        "When a date-time field holds ISO-8601 text with a Z, an offset or no zone (with or without fractional seconds), then it is read as the UTC instant for DateTime, DateTimeOffset and TryParseIso, because strict ISO parsing must still accept every ISO form the API and stored data use.")]
    [InlineData("Z", 0, false)]
    [InlineData("", 0, false)]
    [InlineData("+01:00", 1, false)]
    [InlineData("-05:00", -5, false)]
    [InlineData("Z", 0, true)]
    [InlineData("+01:00", 1, true)]
    [InlineData("", 0, true)]
    public void iso_forms_are_read_as_utc(string zone, int offsetHours, bool fractional)
    {
        // Arrange
        var value = CreateUtcInstant().AddTicks(fractional ? 1234567 : 0);
        var wallClock = value.AddHours(offsetHours);
        var text = $"{wallClock:yyyy-MM-ddTHH:mm:ss}{(fractional ? $"{wallClock:.fffffff}" : string.Empty)}{zone}";
        var json = $"\"{text}\"";

        // Act
        var readDateTime = JsonSerializer.Deserialize<DateTime>(json, _options);
        var readDateTimeOffset = JsonSerializer.Deserialize<DateTimeOffset>(json, _options);
        var parsed = UtcDateTimes.TryParseIso(text, out var fromText);

        // Assert
        readDateTime.Should().Be(value);
        readDateTime.Kind.Should().Be(DateTimeKind.Utc);
        readDateTimeOffset.UtcDateTime.Should().Be(value);
        readDateTimeOffset.Offset.Should().Be(TimeSpan.Zero);
        parsed.Should().BeTrue();
        fromText.Should().Be(value);
        fromText.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory(DisplayName =
        "When offset text falls in the UK repeated October hour, then each occurrence is read as its own UTC instant, because an explicit offset must never be re-read through the host's time zone.")]
    [InlineData("+01:00", 0)]
    [InlineData("+00:00", 1)]
    public void offset_in_repeated_october_hour_keeps_its_instant(string offset, int utcHour)
    {
        // Arrange
        var year = DomainTestFixture.UtcToday.Year + 1;
        var lastDay = new DateTime(year, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var changeDay = lastDay.AddDays(-(int)lastDay.DayOfWeek);
        var expected = changeDay.AddHours(utcHour).AddMinutes(30);
        var text = $"{changeDay:yyyy-MM-dd}T01:30:00{offset}";

        // Act
        var read = JsonSerializer.Deserialize<DateTime>($"\"{text}\"", _options);
        var parsed = UtcDateTimes.TryParseIso(text, out var fromText);

        // Assert
        read.Should().Be(expected);
        read.Kind.Should().Be(DateTimeKind.Utc);
        parsed.Should().BeTrue();
        fromText.Should().Be(expected);
    }

    [Fact(DisplayName =
        "When exported UTC text is parsed with TryParseIso, then zone-less text is UTC, offset text is adjusted to UTC and blank text fails, because every reader of exported date-times must agree.")]
    public void try_parse_iso_reads_utc()
    {
        // Arrange
        var value = CreateUtcInstant();
        var zoneLess = $"{value:yyyy-MM-ddTHH:mm:ss}";
        var withOffset = new DateTimeOffset(value).ToOffset(TimeSpan.FromHours(_fixture.Create<int>() % 12 + 1))
            .ToString("O");

        // Act
        var zoneLessOk = UtcDateTimes.TryParseIso(zoneLess, out var fromZoneLess);
        var offsetOk = UtcDateTimes.TryParseIso(withOffset, out var fromOffset);
        var blankOk = UtcDateTimes.TryParseIso(string.Empty, out _);

        // Assert
        zoneLessOk.Should().BeTrue();
        fromZoneLess.Should().Be(value);
        fromZoneLess.Kind.Should().Be(DateTimeKind.Utc);
        offsetOk.Should().BeTrue();
        fromOffset.Should().Be(value);
        fromOffset.Kind.Should().Be(DateTimeKind.Utc);
        blankOk.Should().BeFalse();
    }

    [Theory(DisplayName =
        "When a Cosmos LINQ filter compares a date-time with a UTC, unspecified-kind or local constant, then the SQL literal is the UTC instant with a Z, because stored date-times are compared as UTC Z text.")]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void cosmos_linq_date_constants_are_utc_z(DateTimeKind kind)
    {
        // Arrange
        var value = CreateUtcInstant();
        var since = kind switch
        {
            DateTimeKind.Local => value.ToLocalTime(),
            _ => DateTime.SpecifyKind(value, kind)
        };
        using var client = new CosmosClient(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(_fixture.CreateGuid().ToByteArray()),
            new CosmosClientOptions { UseSystemTextJsonSerializerWithOptions = _options });
        var container = client.GetContainer("db", "episodes");

        // Act
        var sql = container.GetItemLinqQueryable<Episode>()
            .Where(x => x.ReleaseSort >= since)
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain($"[\"releaseSort\"] >= \"{value:yyyy-MM-ddTHH:mm:ss}Z\"");
    }

    private sealed class NullableDates
    {
        [JsonPropertyName("at")] public DateTime? At { get; init; }

        [JsonPropertyName("atOffset")] public DateTimeOffset? AtOffset { get; init; }
    }

    private sealed class LocalOffsetHolder
    {
        [JsonPropertyName("local")]
        [JsonConverter(typeof(LocalOffsetDateTimeOffsetConverter))]
        public DateTimeOffset Local { get; init; }

        [JsonPropertyName("utc")] public DateTimeOffset Utc { get; init; }
    }
}
