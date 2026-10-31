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

    [Theory(DisplayName =
        "When a Cosmos LINQ filter compares a date-time with a UTC or unspecified-kind constant, then the SQL literal is the UTC instant with a Z, because stored date-times are compared as UTC Z text.")]
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
