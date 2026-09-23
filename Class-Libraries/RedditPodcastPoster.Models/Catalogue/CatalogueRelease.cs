using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Catalogue release stored as a single JSON value:
/// year → number; date → <c>yyyy-MM-dd</c>; datetime → ISO-8601 Zulu.
/// </summary>
[JsonConverter(typeof(CatalogueReleaseJsonConverter))]
public sealed class CatalogueRelease
{
    public CatalogueReleasePrecision Precision { get; init; }

    public int Year { get; init; }

    public DateOnly? Date { get; init; }

    public DateTime? DateTimeUtc { get; init; }

    public static CatalogueRelease FromYear(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        return new CatalogueRelease
        {
            Precision = CatalogueReleasePrecision.Year,
            Year = year
        };
    }

    public static CatalogueRelease FromDate(DateOnly date) => new()
    {
        Precision = CatalogueReleasePrecision.Date,
        Year = date.Year,
        Date = date
    };

    public static CatalogueRelease FromDateTimeUtc(DateTime dateTimeUtc)
    {
        var utc = dateTimeUtc.Kind == DateTimeKind.Utc
            ? dateTimeUtc
            : dateTimeUtc.ToUniversalTime();
        return new CatalogueRelease
        {
            Precision = CatalogueReleasePrecision.DateTimeUtc,
            Year = utc.Year,
            Date = DateOnly.FromDateTime(utc),
            DateTimeUtc = utc
        };
    }
}

public sealed class CatalogueReleaseJsonConverter : JsonConverter<CatalogueRelease>
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeZuluFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

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
                writer.WriteStringValue(utc.ToString(DateTimeZuluFormat, CultureInfo.InvariantCulture));
                break;
            default:
                throw new JsonException($"Unknown CatalogueReleasePrecision '{value.Precision}'.");
        }
    }
}
