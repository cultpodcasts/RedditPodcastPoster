using System.Globalization;
using System.Text.Json;

namespace RedditPodcastPoster.Models.Serialization;

/// <summary>UTC normalisation and parsing shared by the converters and by readers of exported UTC text.</summary>
public static class UtcDateTimes
{
    private const DateTimeStyles UtcStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    /// <summary>The same instant as UTC. <see cref="DateTimeKind.Unspecified"/> is taken to be UTC already.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    /// <summary>Parses an ISO-8601 date-time; no zone means UTC, an offset is adjusted to UTC.</summary>
    /// <exception cref="FormatException">The text is not a date-time.</exception>
    public static DateTime ParseIso(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, UtcStyles);

    /// <summary>As <see cref="ParseIso"/>, without throwing. Returns <c>false</c> for null, blank or invalid text.</summary>
    public static bool TryParseIso(string? text, out DateTime value) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, UtcStyles, out value);

    /// <summary>Reads the current JSON token as a UTC date-time, or throws <see cref="JsonException"/>.</summary>
    internal static DateTime ReadIso(ref Utf8JsonReader reader, string typeName)
    {
        if (reader.TokenType != JsonTokenType.String || !TryParseIso(reader.GetString(), out var value))
        {
            throw new JsonException($"Expected an ISO-8601 date-time string for {typeName}.");
        }

        return value;
    }
}
