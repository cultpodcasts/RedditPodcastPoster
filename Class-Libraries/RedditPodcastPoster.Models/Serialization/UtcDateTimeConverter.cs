using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Serialization;

/// <summary>
/// Reads and writes every <see cref="DateTime"/> as UTC. Writes ISO-8601 with a trailing <c>Z</c>, in the same
/// shape System.Text.Json writes a UTC <see cref="DateTime"/> (fractional seconds only when present), so values
/// that were already UTC are written byte-for-byte as before.
/// <list type="bullet">
/// <item><see cref="DateTimeKind.Utc"/> is written as is.</item>
/// <item><see cref="DateTimeKind.Unspecified"/> is taken to be UTC already (as <c>CatalogueRelease</c> does).</item>
/// <item><see cref="DateTimeKind.Local"/> is converted to UTC.</item>
/// </list>
/// Reading accepts an ISO-8601 date-time string, parsed once by <see cref="UtcDateTimes.ParseIso"/>. A value with no
/// zone is read as UTC, and a value with an offset is adjusted to UTC; the result always has
/// <see cref="DateTimeKind.Utc"/>. System.Text.Json applies this converter to <see cref="Nullable{T}"/> values too,
/// and handles <c>null</c> itself.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcDateTimes.ReadIso(ref reader, nameof(DateTime));

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(UtcDateTimes.ToUtc(value));
}
