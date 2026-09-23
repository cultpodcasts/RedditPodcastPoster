using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Partial release for Film / TvShowEpisode / NewsReport.
/// Not a podcast-episode datetime: precision is year or calendar date only.
/// </summary>
public sealed class CatalogueRelease
{
    [JsonPropertyName("precision")]
    [JsonPropertyOrder(1)]
    public CatalogueReleasePrecision Precision { get; set; }

    /// <summary>Always set. For <see cref="CatalogueReleasePrecision.Date"/>, equals <see cref="Date"/>.Year.</summary>
    [JsonPropertyName("year")]
    [JsonPropertyOrder(2)]
    public int Year { get; set; }

    /// <summary>Set when <see cref="Precision"/> is <see cref="CatalogueReleasePrecision.Date"/>; null for year-only.</summary>
    [JsonPropertyName("date")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? Date { get; set; }

    public static CatalogueRelease FromYear(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        return new CatalogueRelease
        {
            Precision = CatalogueReleasePrecision.Year,
            Year = year,
            Date = null
        };
    }

    public static CatalogueRelease FromDate(DateOnly date) => new()
    {
        Precision = CatalogueReleasePrecision.Date,
        Year = date.Year,
        Date = date
    };
}
