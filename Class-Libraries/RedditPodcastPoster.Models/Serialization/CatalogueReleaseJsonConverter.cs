using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Models.Serialization;

public sealed class CatalogueReleaseJsonConverter : JsonConverter<CatalogueRelease>
{
    private const string DateFormat = "yyyy-MM-dd";
    /// <summary>Zulu datetime with optional fractional seconds (up to 7 digits) so write does not truncate.</summary>
    private const string DateTimeZuluFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    public override CatalogueRelease? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (!reader.TryGetInt32(out var year))
                {
                    throw new JsonException("CatalogueRelease year must be a 32-bit integer.");
                }

                return CatalogueRelease.FromYear(year);
            case JsonTokenType.String:
            {
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new JsonException("CatalogueRelease string value must not be empty.");
                }

                if (DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return CatalogueRelease.FromDate(date);
                }

                if (DateTime.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var dateTime))
                {
                    return CatalogueRelease.FromDateTimeUtc(dateTime);
                }

                throw new JsonException($"CatalogueRelease string '{text}' is not a date or ISO-8601 datetime.");
            }
            default:
                throw new JsonException($"Unexpected token {reader.TokenType} for CatalogueRelease.");
        }
    }

    public override void Write(Utf8JsonWriter writer, CatalogueRelease value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        switch (value.Precision)
        {
            case CatalogueReleasePrecision.Year:
                writer.WriteNumberValue(value.Year);
                break;
            case CatalogueReleasePrecision.Date:
                if (value.Date is null)
                {
                    throw new JsonException("CatalogueRelease Date precision requires Date.");
                }

                writer.WriteStringValue(value.Date.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
                break;
            case CatalogueReleasePrecision.DateTimeUtc:
                if (value.DateTimeUtc is null)
                {
                    throw new JsonException("CatalogueRelease DateTimeUtc precision requires DateTimeUtc.");
                }

                var utc = value.DateTimeUtc.Value.Kind == DateTimeKind.Utc
                    ? value.DateTimeUtc.Value
                    : value.DateTimeUtc.Value.ToUniversalTime();
                // Preserve sub-second precision when present; omit fractional part for whole seconds
                // so existing whole-second Cosmos values do not grow a dangling ".Z".
                var format = utc.Ticks % TimeSpan.TicksPerSecond == 0
                    ? "yyyy-MM-dd'T'HH:mm:ss'Z'"
                    : DateTimeZuluFormat;
                writer.WriteStringValue(utc.ToString(format, CultureInfo.InvariantCulture));
                break;
            default:
                throw new JsonException($"Unknown CatalogueReleasePrecision '{value.Precision}'.");
        }
    }
}
