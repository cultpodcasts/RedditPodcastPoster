using System.Text;
using System.Text.Json;

namespace RedditPodcastPoster.Models.Serialization;

/// <summary>
/// UTC normalisation and parsing shared by the converters and by readers of exported UTC text.
/// A valid date-time is defined in one place: System.Text.Json's ISO-8601 profile
/// (<see cref="Utf8JsonReader.TryGetDateTime"/>), so culture-dependent text such as <c>10/11/2026</c>,
/// <c>Oct 31 2026</c> or a bare <c>12:30</c> is rejected rather than guessed.
/// </summary>
public static class UtcDateTimes
{
    /// <summary>The same instant as UTC. <see cref="DateTimeKind.Unspecified"/> is taken to be UTC already.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    /// <summary>
    /// Parses ISO-8601 text (the System.Text.Json profile): no zone means UTC, an offset is adjusted to UTC.
    /// Returns <c>false</c> for null, blank or non-ISO text.
    /// </summary>
    public static bool TryParseIso(string? text, out DateTime value)
    {
        value = default;
        if (string.IsNullOrEmpty(text) || text.AsSpan().IndexOfAny('"', '\\') >= 0)
        {
            return false;
        }

        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes($"\"{text}\""));
            return reader.Read() && TryRead(ref reader, out value);
        }
        catch (JsonException)
        {
            // Not a valid JSON string (for example a control character), so not an ISO date-time either.
            return false;
        }
    }

    /// <summary>Reads the current JSON token as a UTC date-time, or throws <see cref="JsonException"/>.</summary>
    internal static DateTime ReadIso(ref Utf8JsonReader reader, string typeName) =>
        TryRead(ref reader, out var value)
            ? value
            : throw new JsonException($"Expected an ISO-8601 date-time string for {typeName}.");

    private static bool TryRead(ref Utf8JsonReader reader, out DateTime value)
    {
        value = default;
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTime(out var raw))
        {
            return false;
        }

        switch (raw.Kind)
        {
            case DateTimeKind.Unspecified:
                // No zone in the text: a stored or exported date-time with no zone is UTC.
                value = DateTime.SpecifyKind(raw, DateTimeKind.Utc);
                return true;
            case DateTimeKind.Utc:
                value = raw;
                return true;
            default:
                // An explicit offset: System.Text.Json has converted it to host-local time. Take the instant from the
                // offset form instead, so the result never round-trips through the host's time zone (and its DST
                // ambiguity). Same parser, same text, so the two reads cannot disagree on validity.
                if (!reader.TryGetDateTimeOffset(out var withOffset))
                {
                    return false;
                }

                value = withOffset.UtcDateTime;
                return true;
        }
    }
}
