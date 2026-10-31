using System.Globalization;
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
/// Reading accepts any ISO-8601 date-time System.Text.Json accepts. A value with no zone is read as UTC, and a value
/// with an offset is adjusted to UTC; the result always has <see cref="DateTimeKind.Utc"/>.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTime(out _))
        {
            throw new JsonException($"Expected an ISO-8601 date-time string for {nameof(DateTime)}.");
        }

        return UtcDateTimes.ParseIso(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(UtcDateTimes.ToUtc(value));
}

/// <summary>Reads and writes every <see cref="DateTimeOffset"/> as a UTC instant with a trailing <c>Z</c>.</summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTime(out _))
        {
            throw new JsonException($"Expected an ISO-8601 date-time string for {nameof(DateTimeOffset)}.");
        }

        return new DateTimeOffset(UtcDateTimes.ParseIso(reader.GetString()!), TimeSpan.Zero);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime);
}

/// <summary>Nullable <see cref="DateTime"/>: <c>null</c> stays <c>null</c>, any value is handled by <see cref="UtcDateTimeConverter"/>.</summary>
public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeConverter Inner = new();

    public override bool HandleNull => true;

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeof(DateTime), options);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}

/// <summary>Nullable <see cref="DateTimeOffset"/>: <c>null</c> stays <c>null</c>, any value is handled by <see cref="UtcDateTimeOffsetConverter"/>.</summary>
public sealed class NullableUtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    private static readonly UtcDateTimeOffsetConverter Inner = new();

    public override bool HandleNull => true;

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeof(DateTimeOffset), options);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}

/// <summary>UTC normalisation shared by the converters.</summary>
public static class UtcDateTimes
{
    /// <summary>The same instant as UTC. <see cref="DateTimeKind.Unspecified"/> is taken to be UTC already.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    /// <summary>Parses an ISO-8601 date-time; no zone means UTC, an offset is adjusted to UTC.</summary>
    public static DateTime ParseIso(string text) =>
        DateTime.Parse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}

/// <summary>Registers the UTC date-time converters on a set of serializer options.</summary>
public static class UtcDateTimeJsonSerializerOptionsExtensions
{
    /// <summary>
    /// Adds the UTC <see cref="DateTime"/> / <see cref="DateTimeOffset"/> converters (and nullable versions) unless
    /// they are already present. Property-level <c>[JsonConverter]</c> attributes still win.
    /// </summary>
    public static JsonSerializerOptions AddUtcDateTimeConverters(this JsonSerializerOptions options)
    {
        AddIfMissing<UtcDateTimeConverter>(options);
        AddIfMissing<NullableUtcDateTimeConverter>(options);
        AddIfMissing<UtcDateTimeOffsetConverter>(options);
        AddIfMissing<NullableUtcDateTimeOffsetConverter>(options);
        return options;
    }

    private static void AddIfMissing<T>(JsonSerializerOptions options) where T : JsonConverter, new()
    {
        if (!options.Converters.Any(c => c is T))
        {
            options.Converters.Add(new T());
        }
    }
}
