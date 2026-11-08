using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Serialization;

/// <summary>
/// The one deliberate exception to "the API always emits UTC": a <see cref="DateTimeOffset"/> that stands for a
/// user-chosen local wall-clock time (today only the discovery schedule's <c>slotStartUk</c>) is written with its own
/// UTC offset, e.g. <c>2026-07-23T09:00:00+01:00</c> in BST and <c>2026-12-01T08:00:00+00:00</c> in GMT, so the
/// schedule keeps the curator's local time across daylight-saving changes. Apply it with a property-level
/// <c>[JsonConverter(typeof(LocalOffsetDateTimeOffsetConverter))]</c>; a property attribute wins over the UTC converters
/// registered on the serializer options (<see cref="UtcDateTimeJsonSerializerOptionsExtensions"/>), so the offset is not
/// flattened to <c>Z</c>. Reading keeps the offset that was sent.
/// </summary>
public sealed class LocalOffsetDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTimeOffset(out var value))
        {
            throw new JsonException($"Expected an ISO-8601 date-time string for {nameof(DateTimeOffset)}.");
        }

        return value;
    }

    // Utf8JsonWriter writes a DateTimeOffset with its own offset, including "+00:00" (never "Z") in GMT.
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
