using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Serialization;

/// <summary>
/// Reads and writes every <see cref="DateTimeOffset"/> as a UTC instant with a trailing <c>Z</c>.
/// System.Text.Json applies this converter to <see cref="Nullable{T}"/> values too, and handles <c>null</c> itself.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(UtcDateTimes.ReadIso(ref reader, nameof(DateTimeOffset)), TimeSpan.Zero);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime);
}
