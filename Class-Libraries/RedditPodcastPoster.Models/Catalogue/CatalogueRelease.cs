using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Serialization;

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
