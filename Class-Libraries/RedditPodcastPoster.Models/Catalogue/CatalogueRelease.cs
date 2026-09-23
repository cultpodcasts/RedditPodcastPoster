using RedditPodcastPoster.Models.Serialization;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Catalogue release stored as a single JSON value:
/// year → number; date → <c>yyyy-MM-dd</c>; datetime → ISO-8601 Zulu.
/// Construction is exclusive: factories store exactly one precision; coarser
/// values (<see cref="Year"/>, <see cref="Date"/>) are derived, never competing facts.
/// JSON uses <see cref="CatalogueReleaseJsonConverter"/> (factories), not a public ctor.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(CatalogueReleaseJsonConverter))]
public sealed class CatalogueRelease
{
    private readonly int _year;
    private readonly DateOnly? _date;
    private readonly DateTime? _dateTimeUtc;

    private CatalogueRelease(
        CatalogueReleasePrecision precision,
        int year,
        DateOnly? date,
        DateTime? dateTimeUtc)
    {
        Precision = precision;
        _year = year;
        _date = date;
        _dateTimeUtc = dateTimeUtc;
    }

    public CatalogueReleasePrecision Precision { get; }

    /// <summary>Calendar year of the release. Always present, derived from the stored precision.</summary>
    public int Year => Precision switch
    {
        CatalogueReleasePrecision.Year => _year,
        CatalogueReleasePrecision.Date => _date!.Value.Year,
        CatalogueReleasePrecision.DateTimeUtc => _dateTimeUtc!.Value.Year,
        _ => throw new InvalidOperationException($"Unknown CatalogueReleasePrecision '{Precision}'.")
    };

    /// <summary>Calendar date when precision is Date or DateTimeUtc; otherwise null.</summary>
    public DateOnly? Date => Precision switch
    {
        CatalogueReleasePrecision.Date => _date,
        CatalogueReleasePrecision.DateTimeUtc => DateOnly.FromDateTime(_dateTimeUtc!.Value),
        _ => null
    };

    /// <summary>UTC instant when precision is DateTimeUtc; otherwise null.</summary>
    public DateTime? DateTimeUtc =>
        Precision == CatalogueReleasePrecision.DateTimeUtc ? _dateTimeUtc : null;

    public static CatalogueRelease FromYear(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        return new CatalogueRelease(CatalogueReleasePrecision.Year, year, date: null, dateTimeUtc: null);
    }

    public static CatalogueRelease FromDate(DateOnly date) =>
        new(CatalogueReleasePrecision.Date, year: 0, date, dateTimeUtc: null);

    public static CatalogueRelease FromDateTimeUtc(DateTime dateTimeUtc)
    {
        var utc = dateTimeUtc.Kind == DateTimeKind.Utc
            ? dateTimeUtc
            : dateTimeUtc.ToUniversalTime();
        return new CatalogueRelease(CatalogueReleasePrecision.DateTimeUtc, year: 0, date: null, utc);
    }
}
