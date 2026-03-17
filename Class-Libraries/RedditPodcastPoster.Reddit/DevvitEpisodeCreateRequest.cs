using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Reddit;

public class DevvitEpisodeCreateRequest
{
    [JsonPropertyName("podcastName")]
    public string PodcastName { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("releaseDateTime")]
    [JsonConverter(typeof(RoundTripDateTimeConverter))]
    public DateTime ReleaseDateTime { get; set; }

    [JsonPropertyName("duration")]
    [JsonConverter(typeof(DevvitDurationTimeSpanConverter))]
    public TimeSpan Duration { get; set; }

    [JsonPropertyName("subredditName")]
    public string? SubredditName { get; set; }

    [JsonPropertyName("flairId")]
    public Guid? FlairId { get; set; }

    [JsonPropertyName("flairText")]
    public string? FlairText { get; set; }

    [JsonPropertyName("imageUrl")]
    public Uri? ImageUrl { get; set; }

    [JsonPropertyName("serviceLinks")]
    public DevvitServiceLinks ServiceLinks { get; set; } = new();
}

public class DevvitServiceLinks
{
    [JsonPropertyName("youtube")]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("spotify")]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple_podcasts")]
    public Uri? Apple { get; set; }
}

public class RoundTripDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }
}

public class DevvitDurationTimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.Zero;
        }

        if (TimeSpan.TryParseExact(value, @"\[h\:mm\:ss\]", CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        throw new JsonException($"Invalid duration value '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture));
    }
}

